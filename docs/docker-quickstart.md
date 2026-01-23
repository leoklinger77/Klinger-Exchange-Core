# Guia Rápido - Docker

## 🐳 Rodar com Visual Studio

### Opção 1: Docker Compose (Recomendado)

1. **Abrir no Visual Studio 2022**
   - Abrir `KlingerTrader.sln`

2. **Selecionar Docker Compose como startup**
   - Clique na seta dropdown próximo ao botão Start
   - Selecione "Docker Compose"
   - Clique em Start (F5)

3. **Aguardar build e start**
   - Visual Studio fará build das images automaticamente
   - Container será iniciado e debugger anexado

### Opção 2: Container Individual

1. **Abrir projeto específico**
   - Clicar com botão direito em `KlingerExchange`
   - Selecionar "Set as Startup Project"

2. **Selecionar profile Docker**
   - Dropdown próximo ao Start
   - Selecionar "Container (Dockerfile)"
   - Clique em Start (F5)

---

## 🚀 Rodar com Docker CLI (Linha de Comando)

### Build Images

```powershell
# Na raiz do projeto
cd D:\develop\TimePasses\Trader\Klinger-Exchange-Core

# Build usando docker-compose
docker-compose build

# Ou build individual
docker build -f Exchange/KlingerExchange/Dockerfile -t klinger-exchange:latest .
docker build -f Exchange/KlingerApi/Dockerfile -t klinger-api:latest .
```

### Start Containers

```powershell
# Start com docker-compose (todos os serviços)
docker-compose up -d

# Verificar status
docker-compose ps

# Ver logs
docker-compose logs -f exchange

# Stop
docker-compose down
```

### Start Container Individual

```powershell
# KlingerExchange
docker run -d `
    --name klinger-exchange `
    -p 9823:9823 `
    -p 9824:9824 `
    -p 9900:9900/udp `
    -v ${PWD}/Exchange/KlingerExchange/Config:/app/Config:ro `
    -v ${PWD}/Exchange/KlingerExchange/Matching:/app/Matching:ro `
    -v klinger-data:/app/data `
    klinger-exchange:latest

# Verificar logs
docker logs -f klinger-exchange

# Stop
docker stop klinger-exchange
docker rm klinger-exchange
```

---

## 🐛 Troubleshooting

### Erro: "The project doesn't know how to run the profile with name 'Container (Dockerfile)'"

**Solução:**

1. **Instalar Docker Desktop para Windows**
   - Download: https://www.docker.com/products/docker-desktop/
   - Instalar e reiniciar o computador
   - Iniciar Docker Desktop

2. **Habilitar integração com Visual Studio**
   - Docker Desktop → Settings → General
   - Marcar "Use the WSL 2 based engine"
   - Docker Desktop → Settings → Resources → WSL Integration
   - Habilitar integração

3. **Verificar extensão Docker no Visual Studio**
   - Visual Studio → Extensions → Manage Extensions
   - Procurar "Docker"
   - Instalar "Container Tools" (se não instalado)

4. **Rebuild projeto**
   - Clean Solution
   - Rebuild Solution
   - Tentar novamente

### Erro: "Cannot connect to Docker daemon"

**Solução:**

```powershell
# Verificar se Docker está rodando
docker version

# Se não estiver, iniciar Docker Desktop
# Ou iniciar serviço manualmente (PowerShell Admin)
Start-Service docker
```

### Erro: Port already in use

**Solução:**

```powershell
# Ver quem está usando a porta
netstat -ano | findstr :9823

# Matar processo (PowerShell Admin)
Stop-Process -Id <PID> -Force

# Ou mudar porta no docker-compose.yml
# ports:
#   - "9825:9823"  # Host:Container
```

### Erro: "Access denied" ao montar volumes

**Solução:**

Docker Desktop → Settings → Resources → File Sharing
- Adicionar `D:\develop\TimePasses\Trader` à lista de shared folders
- Apply & Restart

### Container inicia mas não responde

**Verificar logs:**

```powershell
# Logs do container
docker logs klinger-exchange

# Logs em tempo real
docker logs -f klinger-exchange

# Inspecionar container
docker inspect klinger-exchange

# Entrar no container (debug)
docker exec -it klinger-exchange /bin/bash
# ou
docker exec -it klinger-exchange /bin/sh
```

### Performance degradada no Docker

**No Windows com Docker Desktop:**

Docker usa WSL2, que pode ter overhead. Para melhor performance:

1. **Aumentar recursos do WSL2**
   - Criar `C:\Users\<usuario>\.wslconfig`:
   
   ```ini
   [wsl2]
   memory=8GB
   processors=4
   ```

2. **Não usar CPU pinning no Docker (Windows)**
   - Thread affinity não funciona bem em WSL2
   - Remover `--cpuset-cpus` dos comandos docker run

3. **Para performance máxima, rodar nativo no Windows**
   - Docker adiciona ~10-30% overhead
   - Use Docker para desenvolvimento, bare metal para produção

---

## 📊 Verificar Performance

### Dentro do Container

```powershell
# Entrar no container
docker exec -it klinger-exchange /bin/bash

# Ver processos
ps aux

# Ver consumo de CPU
top

# Ver logs de performance
tail -f /app/logs/klinger-exchange-*.log | grep "Min="
```

### Do Host (Windows)

```powershell
# Ver estatísticas do container
docker stats klinger-exchange

# Ver CPU/Memory usage
docker top klinger-exchange

# Ver network
docker exec klinger-exchange netstat -tuln
```

---

## 🔧 Desenvolvimento com Live Reload

### Docker Compose Override

Criar `docker-compose.override.yml`:

```yaml
version: '3.8'

services:
  exchange:
    build:
      target: base  # Use base stage (mais rápido)
    volumes:
      - ./Exchange/KlingerExchange:/src:ro  # Mount source code
    environment:
      - DOTNET_ENVIRONMENT=Development
      - DOTNET_USE_POLLING_FILE_WATCHER=true
```

Isso permite:
- ✅ Build mais rápido (não refaz tudo)
- ✅ Hot reload de configurações
- ✅ Debugging via Visual Studio

---

## 📦 Deploy de Imagem

### Salvar Imagem

```powershell
# Exportar para arquivo
docker save klinger-exchange:latest -o klinger-exchange.tar

# Carregar em outro host
docker load -i klinger-exchange.tar
```

### Push para Registry

```powershell
# Tag com registry
docker tag klinger-exchange:latest myregistry.azurecr.io/klinger-exchange:1.0

# Login
docker login myregistry.azurecr.io

# Push
docker push myregistry.azurecr.io/klinger-exchange:1.0
```

---

## ✅ Checklist Rápido

Antes de rodar no Docker:

- [ ] Docker Desktop instalado e rodando
- [ ] WSL2 habilitado (Settings → General)
- [ ] Shared folders configurados (Settings → Resources)
- [ ] Visual Studio com Container Tools instalado
- [ ] Solution buildada com sucesso (`dotnet build`)
- [ ] Portas 9823, 9824, 5000 livres
- [ ] Arquivos Config/ e Matching/ existem

Após rodar:

- [ ] Container iniciou: `docker ps | grep klinger`
- [ ] Logs sem erros: `docker logs klinger-exchange`
- [ ] FIX porta aberta: `docker exec klinger-exchange netstat -tuln | grep 9823`
- [ ] Performance aceitável: `docker logs klinger-exchange | grep "Min="`

---

**Status:** ✅ Pronto para rodar no Docker!
