FROM microsoft/dotnet:2.2-aspnetcore-runtime-alpine AS base
WORKDIR /app
EXPOSE 8000
#EXPOSE 443
RUN apk add --no-cache \
        bash \
        git \
		icu-libs \
		openssh-keygen \
		openssh
        #openssl
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

FROM microsoft/dotnet:2.2-sdk-alpine AS build
WORKDIR /src
COPY [".", "SqlSCM/"]
RUN dotnet restore "SqlSCM/SqlSCM.csproj"
COPY . .
WORKDIR "/src/SqlSCM"
RUN dotnet build "SqlSCM.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "SqlSCM.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY ["SqlSCM.xml", "."]

COPY --from=publish /app .
RUN rm appsettings.json
ENTRYPOINT ["dotnet", "SqlSCM.dll"]
