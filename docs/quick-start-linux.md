# Quick Start - Linux Deployment

Guia rápido para deployment do KlingerExchange em Linux com performance otimizada.

## ⚡ TL;DR - Deploy em 5 Minutos

```bash
# 1. Clone e build
git clone <repository-url> && cd Klinger-Exchange-Core
dotnet build -c Release

# 2. Configure sistema (requer sudo)
sudo bash -c 'echo never > /sys/kernel/mm/transparent_hugepage/enabled'
sudo sysctl -w vm.swappiness=0
for cpu in /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor; do
    echo performance | sudo tee $cpu > /dev/null
done

# 3. Adicione capability
sudo setcap cap_sys_nice=+ep $(which dotnet)

# 4. Execute
chmod +x run-exchange-linux.sh
./run-exchange-linux.sh
```

---

## 📦 Deployment por Ambiente

### 🏠 Desenvolvimento Local

```bash
# Build e execução simples
dotnet build -c Release
cd Exchange/KlingerExchange/bin/Release/net10.0
dotnet KlingerExchange.dll

# Performance esperada: Min ~8-15µs (sem otimizações)
```

### 🏢 Staging (Docker)

```bash
# Build image
docker build -f Exchange/KlingerExchange/Dockerfile.optimized \
    -t klinger-exchange:staging .

# Run com CPU pinning
docker run -d \
    --name klinger-staging \
    --cap-add=SYS_NICE \
    --cpuset-cpus="2,4" \
    --memory="2g" \
    --network host \
    -v $(pwd)/Exchange/KlingerExchange/Config:/app/Config:ro \
    -v $(pwd)/data:/app/data \
    klinger-exchange:staging

# Logs
docker logs -f klinger-staging

# Performance esperada: Min ~5-8µs
```

### 🚀 Produção (Bare Metal)

```bash
# 1. Configure kernel (requer reboot)
sudo nano /etc/default/grub
# Adicione: isolcpus=2,4 nohz_full=2,4 rcu_nocbs=2,4
sudo update-grub
sudo reboot

# 2. Após reboot, configure sistema
sudo bash -c 'cat > /etc/sysctl.d/99-klinger.conf << EOF
vm.swappiness=0
vm.max_map_count=262144
net.core.rmem_max=134217728
net.core.wmem_max=134217728
EOF'
sudo sysctl -p /etc/sysctl.d/99-klinger.conf

# 3. Deploy aplicação
cd /opt/klinger
dotnet build -c Release
sudo setcap cap_sys_nice=+ep $(which dotnet)

# 4. Crie systemd service (opcional)
sudo nano /etc/systemd/system/klinger-exchange.service

# 5. Execute
./run-exchange-linux.sh --isolated

# Performance esperada: Min ~3-5µs
```

---

## 🔧 Systemd Service (Produção)

### Criar Service Unit

```bash
sudo nano /etc/systemd/system/klinger-exchange.service
```

```ini
[Unit]
Description=KlingerExchange Ultra-Low Latency Trading System
After=network.target

[Service]
Type=simple
User=klinger
Group=klinger
WorkingDirectory=/opt/klinger/Exchange/KlingerExchange/bin/Release/net10.0

# Environment variables
Environment="DOTNET_GCServer=1"
Environment="DOTNET_GCConcurrent=0"
Environment="DOTNET_TieredCompilation=0"
Environment="DOTNET_ReadyToRun=1"

# NUMA binding e CPU affinity
ExecStart=/usr/bin/numactl --cpunodebind=0 --membind=0 --physcpubind=2,4 \
    /usr/bin/nice -n -20 /usr/bin/dotnet KlingerExchange.dll

# Process limits
LimitNOFILE=65536
LimitMEMLOCK=infinity
Nice=-20

# Restart policy
Restart=on-failure
RestartSec=5s

[Install]
WantedBy=multi-user.target
```

### Habilitar e Iniciar

```bash
# Recarregar systemd
sudo systemctl daemon-reload

# Habilitar auto-start
sudo systemctl enable klinger-exchange

# Iniciar serviço
sudo systemctl start klinger-exchange

# Verificar status
sudo systemctl status klinger-exchange

# Logs em tempo real
sudo journalctl -u klinger-exchange -f
```

---

## 🐳 Docker Compose (Multi-Component)

### docker-compose.yml

```yaml
version: '3.8'

services:
  exchange:
    build:
      context: .
      dockerfile: Exchange/KlingerExchange/Dockerfile.optimized
    container_name: klinger-exchange
    cap_add:
      - SYS_NICE
      - IPC_LOCK
    cpuset: "2,4"
    mem_limit: 2g
    network_mode: host
    volumes:
      - ./Exchange/KlingerExchange/Config:/app/Config:ro
      - ./Exchange/KlingerExchange/Matching:/app/Matching:ro
      - exchange-data:/app/data
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "100m"
        max-file: "5"

  api:
    build:
      context: .
      dockerfile: Exchange/KlingerApi/Dockerfile
    container_name: klinger-api
    ports:
      - "5000:8080"
    depends_on:
      - exchange
    restart: unless-stopped

  simulator:
    build:
      context: .
      dockerfile: Exchange/KlingerMarketSimulator/Dockerfile
    container_name: klinger-simulator
    network_mode: host
    depends_on:
      - exchange
    restart: unless-stopped

volumes:
  exchange-data:
```

### Usar Docker Compose

```bash
# Build e start
docker-compose up -d

# Logs
docker-compose logs -f exchange

# Stop
docker-compose down

# Rebuild
docker-compose build --no-cache
docker-compose up -d
```

---

## 📊 Health Check & Monitoring

### Script de Health Check

```bash
#!/bin/bash
# health-check.sh

# Check se processo está rodando
if ! pgrep -f KlingerExchange > /dev/null; then
    echo "ERROR: KlingerExchange not running"
    exit 1
fi

# Check FIX port
if ! netstat -tuln | grep -q ":9823"; then
    echo "ERROR: FIX port 9823 not listening"
    exit 1
fi

# Check latency (ler última linha do log)
LAST_METRIC=$(tail -n 50 logs/klinger-exchange-*.log | grep "Min=" | tail -1)
MIN_LATENCY=$(echo $LAST_METRIC | grep -oP 'Min=\K[\d.]+')

if (( $(echo "$MIN_LATENCY > 20" | bc -l) )); then
    echo "WARNING: High latency detected: ${MIN_LATENCY}µs"
    exit 1
fi

echo "OK: System healthy. Min latency: ${MIN_LATENCY}µs"
exit 0
```

```bash
chmod +x health-check.sh
./health-check.sh
```

### Prometheus Metrics (Futuro)

Endpoints planejados:
- `/metrics` - Prometheus format
- `/health` - Health check JSON
- `/stats` - Performance statistics

---

## 🔄 Upgrade Strategy

### Zero-Downtime Deployment

```bash
#!/bin/bash
# deploy-upgrade.sh

# 1. Build nova versão
git pull
dotnet build -c Release

# 2. Health check atual
./health-check.sh || exit 1

# 3. Drain connections (enviar cancel para ordens pendentes)
# TODO: Implementar admin command

# 4. Stop gracefully
sudo systemctl stop klinger-exchange

# 5. Deploy nova versão
sudo cp -r Exchange/KlingerExchange/bin/Release/net10.0/* /opt/klinger/

# 6. Start
sudo systemctl start klinger-exchange

# 7. Wait for healthy
sleep 5
./health-check.sh
```

---

## 🐛 Debugging em Produção

### Verificar Thread Affinity

```bash
# Encontrar PID
PID=$(pgrep -f KlingerExchange)

# Listar threads e cores
ps -eLo pid,tid,psr,comm | grep $PID

# Saída esperada:
#  PID   TID  PSR COMMAND
# 1234  1235   4  KlingerExchange  <- Matching thread
# 1234  1236   2  KlingerExchange  <- Market data thread
```

### Verificar CPU Usage

```bash
# Top por thread
top -H -p $(pgrep -f KlingerExchange)

# Cores 2,4 devem estar ~100%
```

### Verificar Latência em Tempo Real

```bash
# Tail logs com grep
tail -f logs/klinger-exchange-*.log | grep --line-buffered "Min="

# Ou usar watch
watch -n 1 'tail -n 30 logs/klinger-exchange-*.log | grep "Min=" | tail -1'
```

### Verificar EventStore

```bash
# Tamanho do arquivo
ls -lh data/events_*.dat

# Últimos eventos
hexdump -C data/events_*.dat | tail -n 50
```

---

## 🔒 Security Checklist

- [ ] Executar como usuário não-root (usar capabilities)
- [ ] Firewall configurado (permitir apenas portas 9823, 9824)
- [ ] Logs rotacionados (logrotate)
- [ ] Backups de EventStore configurados
- [ ] Monitoring/alerting habilitado
- [ ] Network isolado (VLAN dedicada recomendada)

---

## 📞 Suporte

### Logs Importantes

```bash
# Application logs
tail -f Exchange/KlingerExchange/logs/klinger-exchange-*.log

# Systemd logs
journalctl -u klinger-exchange -f

# Docker logs
docker logs -f klinger-exchange

# Kernel messages
dmesg -w | grep -i cpu
```

### Performance Profiling

```bash
# dotnet-trace (requer dotnet-tools)
dotnet tool install --global dotnet-trace
dotnet trace collect -p $(pgrep -f KlingerExchange) --duration 00:00:30

# perf (Linux)
sudo perf record -p $(pgrep -f KlingerExchange) -g -- sleep 30
sudo perf report
```

---

## ✅ Validation Checklist

Após deployment, verificar:

- [ ] Processo está rodando: `pgrep -f KlingerExchange`
- [ ] FIX porta aberta: `netstat -tuln | grep 9823`
- [ ] Thread affinity correta: `taskset -cp <PID>`
- [ ] CPU usage alto em cores corretos: `top -H`
- [ ] THP desabilitado: `cat /sys/kernel/mm/transparent_hugepage/enabled`
- [ ] Min latency <10µs: Verificar logs
- [ ] EventStore escrevendo: Verificar tamanho arquivo crescente
- [ ] Market data publicando: `tcpdump -i any udp port 9900`

**Status:** ✅ Pronto para produção!
