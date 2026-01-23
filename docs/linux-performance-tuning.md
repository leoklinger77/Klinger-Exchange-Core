# Linux Performance Tuning - Ultra-Low Latency Guide

Este guia explica como configurar o Linux para rodar o KlingerExchange com a mesma performance ultra-baixa latência do Windows.

## 📋 Índice

1. [Kernel Parameters](#kernel-parameters)
2. [CPU Isolation & Affinity](#cpu-isolation--affinity)
3. [NUMA Configuration](#numa-configuration)
4. [Network Tuning](#network-tuning)
5. [Build & Run](#build--run)
6. [Performance Verification](#performance-verification)

---

## 1. Kernel Parameters

### Transparent Huge Pages (THP)
Desabilitar THP para reduzir latência de page faults:

```bash
# Temporário (perdido no reboot)
echo never | sudo tee /sys/kernel/mm/transparent_hugepage/enabled
echo never | sudo tee /sys/kernel/mm/transparent_hugepage/defrag

# Permanente (adicionar ao /etc/rc.local ou systemd service)
cat <<EOF | sudo tee /etc/systemd/system/disable-thp.service
[Unit]
Description=Disable Transparent Huge Pages (THP)
DefaultDependencies=no
After=sysinit.target local-fs.target
Before=basic.target

[Service]
Type=oneshot
ExecStart=/bin/sh -c 'echo never > /sys/kernel/mm/transparent_hugepage/enabled'
ExecStart=/bin/sh -c 'echo never > /sys/kernel/mm/transparent_hugepage/defrag'

[Install]
WantedBy=basic.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable disable-thp
sudo systemctl start disable-thp
```

### Swappiness
Reduzir swapping para zero:

```bash
# Temporário
sudo sysctl vm.swappiness=0

# Permanente
echo "vm.swappiness=0" | sudo tee -a /etc/sysctl.conf
sudo sysctl -p
```

### Memory-Mapped Files
Aumentar limites de MMF:

```bash
# Temporário
sudo sysctl vm.max_map_count=262144

# Permanente
echo "vm.max_map_count=262144" | sudo tee -a /etc/sysctl.conf
sudo sysctl -p
```

---

## 2. CPU Isolation & Affinity

### CPU Governor
Forçar performance mode (desabilita CPU frequency scaling):

```bash
# Verificar governador atual
cat /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor

# Instalar cpufrequtils
sudo apt-get install cpufrequtils

# Configurar performance mode
echo 'GOVERNOR="performance"' | sudo tee /etc/default/cpufrequtils
sudo systemctl restart cpufrequtils

# Ou manualmente para todos os cores
for cpu in /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor; do
    echo performance | sudo tee $cpu
done
```

### CPU Isolation (isolcpus)
Isolar cores 2 e 4 para uso exclusivo do Exchange:

**⚠️ Aviso:** Isso reserva cores exclusivamente para o processo. Editar `/etc/default/grub`:

```bash
sudo nano /etc/default/grub

# Adicionar à linha GRUB_CMDLINE_LINUX_DEFAULT:
GRUB_CMDLINE_LINUX_DEFAULT="quiet splash isolcpus=2,4 nohz_full=2,4 rcu_nocbs=2,4"

# Aplicar mudanças
sudo update-grub
sudo reboot
```

**Explicação:**
- `isolcpus=2,4`: Remove cores 2,4 do scheduler normal
- `nohz_full=2,4`: Desabilita timer ticks nesses cores (reduz interrupções)
- `rcu_nocbs=2,4`: Move RCU callbacks para outros cores

### Thread Pinning (Método Alternativo)
Se não quiser isolar cores no boot, use `taskset`:

```bash
# Rodar Exchange com affinity em cores 2,4,6
taskset -c 2,4,6 dotnet KlingerExchange.dll

# Ou usando numactl (melhor para NUMA)
numactl --physcpubind=2,4,6 --membind=0 dotnet KlingerExchange.dll
```

---

## 3. NUMA Configuration

### Verificar Topologia NUMA

```bash
# Instalar hwloc
sudo apt-get install hwloc

# Ver topologia
lstopo --no-io

# Ver NUMA nodes
numactl --hardware
```

### Exemplo: AMD Ryzen 7 2700 (8 cores, 1 NUMA node)

```bash
# Pinar processo ao NUMA node 0 e cores específicos
numactl --cpunodebind=0 --membind=0 --physcpubind=2,4 dotnet KlingerExchange.dll
```

### Para sistemas multi-NUMA (e.g., dual-socket Intel)
Se os cores 2,4 estiverem em NUMA nodes diferentes, ajustar:

```bash
# Exemplo: Core 2 em Node 0, Core 4 em Node 1
# Usar apenas Node 0 para consistência
numactl --cpunodebind=0 --membind=0 dotnet KlingerExchange.dll
```

---

## 4. Network Tuning

### Aumentar Buffers UDP

```bash
# Temporário
sudo sysctl -w net.core.rmem_max=134217728
sudo sysctl -w net.core.wmem_max=134217728
sudo sysctl -w net.core.rmem_default=16777216
sudo sysctl -w net.core.wmem_default=16777216

# Permanente
cat <<EOF | sudo tee -a /etc/sysctl.conf
net.core.rmem_max=134217728
net.core.wmem_max=134217728
net.core.rmem_default=16777216
net.core.wmem_default=16777216
net.core.netdev_max_backlog=10000
net.ipv4.udp_mem=8388608 12582912 16777216
EOF

sudo sysctl -p
```

### Desabilitar Offloading (reduz jitter)

```bash
# Para interface eth0 (ajustar conforme necessário)
sudo ethtool -K eth0 gro off
sudo ethtool -K eth0 tso off
sudo ethtool -K eth0 gso off
sudo ethtool -K eth0 sg off
```

### IRQ Affinity (Pinar interrupções de rede)

```bash
# Encontrar IRQ da interface de rede
IRQ=$(cat /proc/interrupts | grep eth0 | awk '{print $1}' | sed 's/://')

# Pinar IRQ para core 0 (longe dos cores 2,4)
echo 1 | sudo tee /proc/irq/$IRQ/smp_affinity
```

---

## 5. Build & Run

### Build Release

```bash
cd /path/to/Klinger-Exchange-Core
dotnet build KlingerTrader.sln -c Release
```

### Rodar com Permissões de Real-Time

**Opção 1: Capabilities (recomendado)**

```bash
# Adicionar capability ao executável
sudo setcap cap_sys_nice=+ep /path/to/KlingerExchange

# Rodar normalmente
dotnet KlingerExchange.dll
```

**Opção 2: Executar como root (não recomendado)**

```bash
sudo dotnet KlingerExchange.dll
```

**Opção 3: Adicionar usuário ao grupo realtime**

```bash
# Criar grupo realtime
sudo groupadd realtime

# Adicionar usuário
sudo usermod -aG realtime $USER

# Editar /etc/security/limits.conf
echo "@realtime soft rtprio 99" | sudo tee -a /etc/security/limits.conf
echo "@realtime hard rtprio 99" | sudo tee -a /etc/security/limits.conf

# Relogar para aplicar mudanças
```

### Rodar com Configuração Otimizada (Completo)

```bash
#!/bin/bash
# run-exchange-optimized.sh

# Cores para matching (4) e market data (2)
CORES="2,4"
NUMA_NODE=0

# Variáveis de ambiente .NET
export DOTNET_GCServer=1
export DOTNET_GCConcurrent=0
export DOTNET_TieredCompilation=0
export DOTNET_ReadyToRun=1
export DOTNET_TC_QuickJitForLoops=1

# Aumentar prioridade do processo
sudo nice -n -20 numactl \
    --cpunodebind=$NUMA_NODE \
    --membind=$NUMA_NODE \
    --physcpubind=$CORES \
    dotnet Exchange/KlingerExchange/bin/Release/net10.0/KlingerExchange.dll
```

Tornar executável:
```bash
chmod +x run-exchange-optimized.sh
./run-exchange-optimized.sh
```

---

## 6. Performance Verification

### Verificar Thread Affinity

```bash
# Em outro terminal, enquanto Exchange roda
PID=$(pgrep -f KlingerExchange)

# Ver threads do processo
ps -eLo pid,tid,psr,comm | grep $PID

# Verificar affinity de thread específica
taskset -cp <TID>
```

Saída esperada:
```
pid: 12345's current affinity list: 4      # Matching thread
pid: 12346's current affinity list: 2      # Market data thread
```

### Verificar CPU Usage

```bash
# Instalar htop
sudo apt-get install htop

# Rodar htop e pressionar F2 -> Display options -> 
# Marcar "Detailed CPU time" e "Show custom thread names"
htop
```

Cores 2 e 4 devem estar 100% utilizados.

### Verificar Latência

No output do Exchange, você deve ver:
```
[INFO] Min=4.6µs Avg=21.3µs P50=12.5µs P95=25.9µs P99=50.7µs Max=13.74ms
```

### Testar Isolamento de Core

```bash
# Stress test em outros cores (não 2,4)
stress-ng --cpu 6 --cpu-method all --timeout 60s --taskset 0,1,3,5,6,7

# Performance do Exchange NÃO deve degradar
```

---

## 🐳 Docker Deployment (Opcional)

### Build Image

```bash
cd /path/to/Klinger-Exchange-Core
docker build -f Exchange/KlingerExchange/Dockerfile.optimized -t klinger-exchange:latest .
```

### Run Container com Configuração Otimizada

```bash
docker run -d \
    --name klinger-exchange \
    --cap-add=SYS_NICE \
    --cap-add=IPC_LOCK \
    --ulimit memlock=-1:-1 \
    --cpuset-cpus="2,4" \
    --memory="2g" \
    --network host \
    -v $(pwd)/Exchange/KlingerExchange/Config:/app/Config:ro \
    -v $(pwd)/Exchange/KlingerExchange/Matching:/app/Matching:ro \
    -v $(pwd)/data:/app/data \
    klinger-exchange:latest
```

**Explicação:**
- `--cap-add=SYS_NICE`: Permite ajustar prioridade de thread
- `--cap-add=IPC_LOCK`: Permite lock de memória (mlockall)
- `--cpuset-cpus="2,4"`: Restringe container aos cores 2,4
- `--memory="2g"`: Limita memória (ajustar conforme necessário)
- `--network host`: Usa stack de rede do host (menor latência)

---

## 📊 Expected Performance

Com as configurações acima, você deve obter:

| Métrica | Windows (baseline) | Linux (otimizado) | Diferença |
|---------|-------------------|-------------------|-----------|
| Min     | 4.6µs             | 3.8-5.5µs         | ±15%      |
| P50     | 12.5µs            | 11-14µs           | ±10%      |
| P95     | 25.9µs            | 24-28µs           | ±8%       |
| P99     | 50.7µs            | 48-55µs           | ±7%       |

**Nota:** Linux pode ser ligeiramente mais rápido devido a menor overhead do kernel em context switching.

---

## 🔍 Troubleshooting

### Latência alta (>100µs)

1. **Verificar CPU governor:** Deve ser `performance`
2. **Verificar swapping:** `sudo swapon -s` deve estar vazio
3. **Verificar THP:** Deve estar `[never]`
4. **Verificar affinity:** Threads devem estar pinadas aos cores corretos

### Permission Denied ao configurar thread priority

```bash
# Adicionar capability
sudo setcap cap_sys_nice=+ep /path/to/KlingerExchange

# Ou usar método do grupo realtime (ver seção 5)
```

### Network multicast não funciona

```bash
# Verificar rota multicast
ip route add 239.0.0.0/8 dev eth0

# Habilitar multicast na interface
sudo ifconfig eth0 multicast
```

---

## 📚 Referências

- [Red Hat Real-Time Tuning Guide](https://access.redhat.com/documentation/en-us/red_hat_enterprise_linux_for_real_time/7/html/tuning_guide/)
- [Linux Kernel NOHZ Full](https://www.kernel.org/doc/Documentation/timers/NO_HZ.txt)
- [.NET Core Performance Best Practices](https://learn.microsoft.com/en-us/dotnet/core/performance/)
- [NUMA Tuning Guide](https://documentation.suse.com/sles/15-SP1/html/SLES-all/cha-tuning-numactl.html)

---

## ✅ Checklist Rápido

- [ ] THP desabilitado (`[never]`)
- [ ] Swappiness = 0
- [ ] CPU governor = `performance`
- [ ] Cores 2,4 isolados (opcional mas recomendado)
- [ ] NUMA binding configurado
- [ ] UDP buffers aumentados
- [ ] Capabilities configuradas (`cap_sys_nice`)
- [ ] Thread affinity verificada com `taskset -cp`
- [ ] Performance baseline validada (<10µs min latency)

**Sucesso!** 🚀 Seu KlingerExchange está otimizado para ultra-baixa latência no Linux!
