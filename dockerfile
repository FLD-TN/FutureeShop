# Phần 1: Build ứng dụng bằng .NET SDK
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Thay "Tên_Project_Của_Bạn" bằng tên file .csproj của bạn (ví dụ: MyApp.csproj)
COPY ["MTKPM_FE.csproj", "./"]
RUN dotnet restore "MTKPM_FE.csproj"

COPY . .
WORKDIR "/src/"
RUN dotnet publish "MTKPM_FE.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Phần 2: Chạy ứng dụng bằng .NET Runtime (nhẹ hơn)
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app
EXPOSE 8080 
COPY --from=build /app/publish .

# Thay "Tên_Project_Của_Bạn" bằng tên file .dll của bạn (ví dụ: MyApp.dll)
ENTRYPOINT ["dotnet", "MTKPM_FE.dll"]