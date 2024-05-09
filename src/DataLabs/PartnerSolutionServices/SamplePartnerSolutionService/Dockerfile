#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG OutputDir="."
WORKDIR /app
EXPOSE 5072
COPY ${OutputDir} .
RUN dotnet tool install --global dotnet-gcdump
RUN dotnet tool install --global dotnet-dump
RUN dotnet tool install --global dotnet-counters
RUN dotnet tool install --global dotnet-trace
RUN dotnet tool install --global dotnet-stack
RUN dotnet tool install --global dotnet-symbol
RUN dotnet tool install --global dotnet-sos
RUN dotnet tool install --global dotnet-monitor

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /root/.dotnet/tools .
COPY --from=build /app .
ENV DOTNET_DbgEnableMiniDump=1
ENTRYPOINT ["dotnet", "SamplePartnerSolutionService.dll"]