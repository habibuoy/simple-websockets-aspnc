# simple-websockets-aspnc

A simple WebSocket demonstration using ASP.NET Core.  
This project consists of two apps: a Server and a Client.

## Server App
The server uses ASP.NET Core as the framework. It handles WebSocket requests through 2 endpoints. Both endpoints only accept a WebSocket request and will return a 400 Bad Request response if they receive anything other. There is no user authentication whatsoever, as this only demonstrates what the WebSocket can do.
- `/ws`  
  This endpoint returns a hello message to the client every 1 second. The client may send any messages to the server. If a client does not send any message within 5 seconds, the Server will assume that the client is disconnected and will close the WebSocket connection.  
  For example: `wss://localhost:7000/ws`.  
- `/wschat?chatRoomId=&user=`  
  This endpoint is where the server handles a chat between two clients. A client must provide a `string user` and `string chatRoomId` query parameter, otherwise, the server will return a 400 Bad Request response. If a client accesses this endpoint and provides a user parameter that is already in a chat, the server will return a 403 Forbidden response. To get a chat room id, a client must access the `/chat` endpoint (refer to point 3). There is no ping check in this endpoint like in point 1.  
  For example: `wss://localhost:7000/wschat?chatRoomId=9155ad85-e356-41b6-bc31-f9bc2055fb0d&user=test`
- `POST /chat?sender=&recipient=`
  This endpoint accepts a regular HTTP request. Clients must provide a `string sender` and `string recipient` query parameter. The sender parameter is just the same as the user parameter in point 2. If the request is valid, the server will return a chat room id, otherwise, it will return a 400 Bad Request response.  
  For example: `POST /chat?sender=test&recipient=test2`  
  Example response
  > ```
  > {
  >   "message": ""
  >   "result": {
  >     "id": "9155ad85-e356-41b6-bc31-f9bc2055fb0d"
  >   }
  > }
  > ```

## Client App
A simple console app. It has 2 features.  
1. WebSocket Test  
   This feature will access the `/ws` endpoint and automatically send a ping message every 500 ms to prevent the server from closing the connection.
2. WebSocket Chat  
   This feature will make the app act as a Chat Client. It will ask for the sender and recipient, and then it will handle connecting to the WebSocket chat endpoint. If the WebSocket chat is successfully established, the user can receive messages from and send messages to another client.
