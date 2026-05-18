FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Chat_API/Chat_API.slnx Chat_API/
COPY Chat_API/Chat_API/Chat_API.csproj Chat_API/Chat_API/
COPY Chat_API/Chatbot_Application/Chatbot_Application.csproj Chat_API/Chatbot_Application/
COPY Chat_API/Chatbot_Domain/Chatbot_Domain.csproj Chat_API/Chatbot_Domain/
COPY Chat_API/Chatbot_Infrastructure/Chatbot_Infrastructure.csproj Chat_API/Chatbot_Infrastructure/

RUN dotnet restore Chat_API/Chat_API/Chat_API.csproj

COPY Chat_API/ Chat_API/
WORKDIR /src/Chat_API/Chat_API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Chat_API.dll"]
