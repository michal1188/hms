#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
FROM mcr.microsoft.com/dotnet/aspnet:3.1 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
ENV TZ=Europe/Warsaw 

FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY ["HMS.csproj", "."]
RUN dotnet restore "./HMS.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "HMS.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HMS.csproj" -c Release -o /app/publish
FROM base AS final
WORKDIR /app/Certificates
COPY Certificates ./
WORKDIR /app/CirrusUpdateFiles
COPY CirrusUpdateFiles ./
WORKDIR /app
COPY ["Security.cs",""]
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "HMS.dll"]