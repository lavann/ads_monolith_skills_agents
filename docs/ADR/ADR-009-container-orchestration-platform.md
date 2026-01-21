# ADR-009: Container Orchestration Platform Selection

**Status**: Proposed  
**Date**: 2025-01-21  
**Context**: Modernisation Planning

## Context

As we containerize the application and extract microservices, we need a platform to:
- Deploy and manage multiple containers
- Provide service discovery and load balancing
- Enable horizontal scaling (replicas)
- Handle container failures (automatic restart)
- Manage configuration and secrets
- Support rolling deployments with zero downtime

We need to choose between **Docker Compose** (simple, development-focused) and **Kubernetes** (production-grade, complex).

## Decision

Adopt a **hybrid approach**:
- **Docker Compose** for local development and simple deployments (Phases 0-2)
- **Kubernetes** for production and advanced scenarios (Phase 3+)

This allows the team to learn gradually without being overwhelmed by Kubernetes complexity upfront.

## Rationale

### Why Docker Compose First?

**1. Simplicity**
- ✅ Single YAML file defines entire stack
- ✅ Minimal learning curve (team already familiar with Docker)
- ✅ Fast iteration (compose up/down in seconds)

**2. Local Development**
- ✅ Perfect for developer workstations
- ✅ Easy to debug (docker logs, docker exec)
- ✅ Consistent environment across team

**3. Small Scale Deployments**
- ✅ Sufficient for staging/demo environments (single server)
- ✅ No infrastructure overhead (no cluster management)

**4. Migration Path**
- ✅ Docker Compose experience translates to Kubernetes concepts
- ✅ Can generate Kubernetes manifests from Compose files (kompose tool)

### Why Kubernetes Eventually?

**1. Production Requirements**
- ✅ High availability (multiple replicas across nodes)
- ✅ Auto-scaling (horizontal pod autoscaling based on CPU/memory)
- ✅ Self-healing (automatic restart of failed containers)
- ✅ Rolling updates (zero-downtime deployments)

**2. Service Management**
- ✅ Built-in service discovery (DNS-based)
- ✅ Load balancing across replicas
- ✅ Health checks (liveness and readiness probes)
- ✅ Secrets management (encrypted at rest)

**3. Observability**
- ✅ Centralized logging (FluentD, Elasticsearch)
- ✅ Metrics collection (Prometheus, Grafana)
- ✅ Distributed tracing (Jaeger, Zipkin)

**4. Cloud-Native**
- ✅ Vendor-neutral (runs on Azure, AWS, GCP, on-premises)
- ✅ Industry standard (CNCF project, largest community)
- ✅ Rich ecosystem (Helm charts, operators, tools)

### Why Not Other Options?

**Docker Swarm**:
- ✅ **Pros**: Simpler than Kubernetes, native Docker integration
- ❌ **Cons**: Less mature ecosystem, fewer features, declining adoption
- **Decision**: Rejected (Kubernetes has won the orchestration wars)

**Nomad (HashiCorp)**:
- ✅ **Pros**: Simpler than Kubernetes, multi-workload (containers, VMs, batch)
- ❌ **Cons**: Smaller community, requires separate service mesh (Consul)
- **Decision**: Rejected (team lacks HashiCorp experience)

**Amazon ECS/Fargate**:
- ✅ **Pros**: AWS-native, no cluster management (Fargate)
- ❌ **Cons**: AWS lock-in, cannot migrate to other clouds
- **Decision**: Rejected (prefer vendor-neutral)

**Azure Container Instances (ACI)**:
- ✅ **Pros**: Serverless containers, simple
- ❌ **Cons**: Limited orchestration features, Azure lock-in
- **Decision**: Rejected (too basic for production)

## Consequences

### Positive

**Docker Compose (Phases 0-2)**:
- ✅ Fast onboarding (team productive immediately)
- ✅ Low operational overhead (no cluster to manage)
- ✅ Easy troubleshooting (familiar Docker commands)

**Kubernetes (Phase 3+)**:
- ✅ Production-ready (battle-tested at scale)
- ✅ Cloud-portable (not locked to vendor)
- ✅ Rich feature set (scaling, rolling updates, secrets)
- ✅ Strong community support (documentation, tools, experts)

### Negative

**Docker Compose**:
- ❌ Not suitable for production (no HA, no auto-scaling)
- ❌ Limited to single host (no multi-node clustering)
- ❌ Manual scaling (must edit replicas in YAML)

**Kubernetes**:
- ❌ Steep learning curve (complex concepts: pods, deployments, services, ingress)
- ❌ Operational complexity (cluster management, networking, storage)
- ❌ Over-engineered for small deployments (overkill for 5 services)

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Team skill gap (Kubernetes)** | High | Medium | Training, pair programming, managed Kubernetes (AKS/EKS) |
| **Kubernetes complexity delays migration** | Medium | Medium | Start with Docker Compose, migrate incrementally |
| **Configuration drift (Compose vs K8s)** | Medium | Low | Use kompose to generate K8s manifests from Compose files |
| **Production issues due to Docker Compose** | Low | High | Use Docker Compose ONLY for dev/staging, never production |

## Implementation

### Phase 0-2: Docker Compose

**docker-compose.yml**:
```yaml
version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - SA_PASSWORD=${SQL_SA_PASSWORD}
      - ACCEPT_EULA=Y
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SQL_SA_PASSWORD} -Q "SELECT 1"
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - retail-network

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    networks:
      - retail-network

  product-service:
    build: ./src/ProductService
    environment:
      - ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=RetailMonolith;User=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=True
      - ConnectionStrings__Redis=redis:6379
    depends_on:
      sqlserver:
        condition: service_healthy
      redis:
        condition: service_started
    networks:
      - retail-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  order-service:
    build: ./src/OrderService
    environment:
      - ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=RetailMonolith;User=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=True
    depends_on:
      sqlserver:
        condition: service_healthy
    networks:
      - retail-network

  api-gateway:
    build: ./src/ApiGateway
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}
    depends_on:
      - product-service
      - order-service
    networks:
      - retail-network

  web-frontend:
    build: ./src/RetailMonolith
    environment:
      - ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}
      - ApiGateway__BaseUrl=http://api-gateway:8080
    depends_on:
      - api-gateway
    ports:
      - "5001:8080"
    networks:
      - retail-network

volumes:
  sqldata:

networks:
  retail-network:
    driver: bridge
```

**Usage**:
```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f product-service

# Scale a service (manual)
docker-compose up -d --scale product-service=3

# Stop all services
docker-compose down

# Rebuild after code changes
docker-compose build product-service
docker-compose up -d product-service
```

### Phase 3+: Kubernetes

**Kubernetes Resources**:

**Namespace**: `retail-system`
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: retail-system
```

**Deployment**: `product-service`
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: product-service
  namespace: retail-system
spec:
  replicas: 3
  selector:
    matchLabels:
      app: product-service
  template:
    metadata:
      labels:
        app: product-service
        version: v1
    spec:
      containers:
      - name: product-service
        image: retailmonolith.azurecr.io/product-service:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: db-connection
              key: connection-string
        - name: ConnectionStrings__Redis
          value: "redis:6379"
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5
      imagePullSecrets:
      - name: acr-secret
```

**Service**: `product-service`
```yaml
apiVersion: v1
kind: Service
metadata:
  name: product-service
  namespace: retail-system
spec:
  selector:
    app: product-service
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8080
  type: ClusterIP
```

**HorizontalPodAutoscaler**: Auto-scaling
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: product-service-hpa
  namespace: retail-system
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: product-service
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
```

**Ingress**: External access
```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: retail-ingress
  namespace: retail-system
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  tls:
  - hosts:
    - retailmonolith.example.com
    secretName: retailmonolith-tls
  rules:
  - host: retailmonolith.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: api-gateway
            port:
              number: 80
```

**ConfigMap**: Non-sensitive configuration
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: product-service-config
  namespace: retail-system
data:
  appsettings.json: |
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information"
        }
      },
      "Redis": {
        "CacheExpirationMinutes": 5
      }
    }
```

**Secret**: Sensitive data (connection strings, API keys)
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: db-connection
  namespace: retail-system
type: Opaque
stringData:
  connection-string: "Server=sqlserver;Database=RetailMonolith;User=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True"
```

**Usage**:
```bash
# Create namespace
kubectl apply -f k8s/namespace.yaml

# Deploy services
kubectl apply -f k8s/product-service/

# View pods
kubectl get pods -n retail-system

# View logs
kubectl logs -f deployment/product-service -n retail-system

# Scale manually (overrides HPA)
kubectl scale deployment/product-service --replicas=5 -n retail-system

# Port forward for local testing
kubectl port-forward service/product-service 8080:80 -n retail-system

# Delete deployment
kubectl delete -f k8s/product-service/
```

## Transition Strategy: Docker Compose → Kubernetes

### Tool: Kompose

**Kompose** converts Docker Compose files to Kubernetes manifests

**Installation**:
```bash
# macOS
brew install kompose

# Linux
curl -L https://github.com/kubernetes/kompose/releases/download/v1.28.0/kompose-linux-amd64 -o kompose
chmod +x kompose
sudo mv kompose /usr/local/bin/
```

**Usage**:
```bash
# Convert docker-compose.yml to Kubernetes manifests
kompose convert -f docker-compose.yml -o k8s/

# Generated files:
# k8s/product-service-deployment.yaml
# k8s/product-service-service.yaml
# k8s/sqlserver-deployment.yaml
# k8s/sqlserver-service.yaml
# ...

# Review and customize generated files (kompose is not perfect)
# Then apply to cluster:
kubectl apply -f k8s/
```

**Note**: Kompose generates basic manifests. You'll need to add:
- Resource limits/requests
- Health checks (liveness/readiness probes)
- HorizontalPodAutoscaler
- Ingress rules
- Secrets (instead of environment variables)

## Managed Kubernetes Options

### Azure Kubernetes Service (AKS)

**Pros**:
- ✅ Managed control plane (Microsoft handles master nodes)
- ✅ Integrated with Azure services (ACR, Key Vault, Monitor)
- ✅ Free control plane (only pay for worker nodes)
- ✅ Auto-upgrades, auto-scaling

**Cons**:
- ❌ Azure lock-in (hard to migrate to other clouds)

**Recommendation**: **Best choice for Azure deployments**

### Amazon EKS

**Pros**:
- ✅ Managed control plane
- ✅ Integrated with AWS services (ECR, IAM, CloudWatch)
- ✅ Extensive ecosystem (AWS-specific tools)

**Cons**:
- ❌ AWS lock-in
- ❌ Control plane costs $0.10/hour (~$73/month)

**Recommendation**: Choose if already on AWS

### Google GKE

**Pros**:
- ✅ Most mature managed Kubernetes (Google invented K8s)
- ✅ Autopilot mode (fully managed nodes)
- ✅ Best developer experience

**Cons**:
- ❌ GCP lock-in
- ❌ Less popular than Azure/AWS in enterprise

**Recommendation**: Choose if already on GCP

### Self-Hosted Kubernetes

**Pros**:
- ✅ Full control
- ✅ No vendor lock-in
- ✅ Cost savings (no managed service fees)

**Cons**:
- ❌ High operational burden (manage control plane, upgrades, backups)
- ❌ Requires Kubernetes expertise

**Recommendation**: **Not recommended** for this project (team lacks K8s experience)

## Validation

### Success Criteria

**Docker Compose (Phases 0-2)**:
- ✅ All services start successfully with `docker-compose up`
- ✅ Services communicate over Docker network
- ✅ Logs accessible via `docker-compose logs`
- ✅ Developers can run entire stack locally

**Kubernetes (Phase 3+)**:
- ✅ All pods healthy (Ready 1/1)
- ✅ HPA scales pods based on load
- ✅ Rolling updates deploy without downtime
- ✅ Failed pods automatically restart (self-healing)
- ✅ External access via Ingress works

### Testing

**Docker Compose**:
```bash
# Smoke test: Start stack and verify health
docker-compose up -d
sleep 30  # Wait for services to start
curl http://localhost:5000/health  # API Gateway
curl http://localhost:5001/health  # Frontend
docker-compose down
```

**Kubernetes**:
```bash
# Smoke test: Deploy and verify
kubectl apply -f k8s/
kubectl wait --for=condition=ready pod -l app=product-service -n retail-system --timeout=60s
kubectl port-forward service/api-gateway 5000:80 -n retail-system &
curl http://localhost:5000/health
kubectl delete -f k8s/
```

## References

- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [Kubernetes Documentation](https://kubernetes.io/docs/)
- [Kompose - Convert Compose to Kubernetes](https://kompose.io/)
- [Azure Kubernetes Service (AKS)](https://azure.microsoft.com/en-us/services/kubernetes-service/)
- [12-Factor App - Backing Services](https://12factor.net/backing-services)

## Related ADRs

- ADR-005: Service Decomposition Strategy
- ADR-007: API Gateway Technology Selection (YARP)
