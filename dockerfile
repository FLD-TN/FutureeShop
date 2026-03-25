# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy file csproj và restore trước (tận dụng layer cache)
COPY ["MTKPM_FE.csproj", "./"]
RUN dotnet restore "MTKPM_FE.csproj"

# Sau đó mới copy toàn bộ code và build
COPY . .
RUN dotnet build "MTKPM_FE.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "MTKPM_FE.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Final image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MTKPM_FE.dll"]