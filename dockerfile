# Sử dụng SDK 6.0 để build
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy file project và restore trước để tận dụng cache
# LƯU Ý: Kiểm tra chính xác tên file MTKPM_FE.csproj ở đây
COPY ["MTKPM_FE.csproj", "./"]
RUN dotnet restore "MTKPM_FE.csproj"

# Copy toàn bộ code còn lại và build
COPY . .
RUN dotnet publish "MTKPM_FE.csproj" -c Release -o out /p:UseAppHost=false

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build /app/out .

# LƯU Ý: Kiểm tra chính xác tên file MTKPM_FE.dll ở đây
ENTRYPOINT ["dotnet", "MTKPM_FE.dll"]