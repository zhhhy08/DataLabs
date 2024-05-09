# Usage:
#  set image "<add-image-name-here>"
#  docker build -t $image -f Dockerfile.socat .

FROM mcr.microsoft.com/cbl-mariner/base/core:2.0@sha256:77651116f2e83cf50fddd8a0316945499f8ce6521ff8e94e67539180d1e5975a
RUN tdnf -y update && \
    tdnf -y install socat && \
    tdnf clean all
ENTRYPOINT ["socat"]
