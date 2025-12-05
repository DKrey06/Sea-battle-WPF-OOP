using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace SeaBattle.Server
{
    public class GameRoom
    {
        public string RoomId { get; set; }
        public ClientHandler Player1 { get; set; }
        public ClientHandler Player2 { get; set; }
        public bool IsFull => Player1 != null && Player2 != null;
        public GameState GameState { get; set; }
        public string CurrentPlayerTurn { get; set; }

        public GameRoom(string roomId)
        {
            RoomId = roomId;
            GameState = new GameState();
        }
    }

    public class GameState
    {
        public Dictionary<string, bool> PlayersReady { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, List<ShipPlacement>> PlayerShips { get; set; } = new Dictionary<string, List<ShipPlacement>>();
        public Dictionary<string, List<Shot>> PlayerShots { get; set; } = new Dictionary<string, List<Shot>>();
        public bool GameStarted => PlayersReady.Count == 2 && PlayersReady.All(p => p.Value);

        public string CheckWinner()
        {
            foreach (var player in PlayerShips.Keys)
            {
                var opponent = PlayerShips.Keys.First(p => p != player);
                var playerShips = PlayerShips[player];
                var opponentShots = PlayerShots.ContainsKey(opponent) ? PlayerShots[opponent] : new List<Shot>();

                bool allShipsSunk = playerShips.All(ship =>
                    ship.Cells.All(cell =>
                        opponentShots.Any(shot => shot.X == cell.X && shot.Y == cell.Y)));

                if (allShipsSunk) return opponent;
            }
            return null;
        }
    }

    public class ShipPlacement
    {
        public int Size { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsVertical { get; set; }
        public List<Cell> Cells { get; set; } = new List<Cell>();
    }

    public class Cell
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class Shot
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsHit { get; set; }
        public string PlayerId { get; set; }
    }

    public class GameMessage
    {
        public string Type { get; set; }
        public string Data { get; set; }
        public string PlayerId { get; set; }
        public string RoomId { get; set; }
    }

    public class ClientHandler
    {
        private TcpClient client;
        private NetworkStream stream;
        private GameServer server;
        public string ClientId { get; set; }
        public string RoomId { get; set; }

        public ClientHandler(TcpClient client, GameServer server)
        {
            this.client = client;
            this.server = server;
            this.stream = client.GetStream();
            ClientId = Guid.NewGuid().ToString();
        }

        public void Start()
        {
            new Thread(() =>
            {
                try
                {
                    byte[] buffer = new byte[4096];
                    while (client.Connected)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            ProcessMessage(message);
                        }
                    }
                }
                catch { server.RemoveClient(this); }
            }).Start();
        }

        private void ProcessMessage(string message)
        {
            try
            {
                var gameMessage = JsonConvert.DeserializeObject<GameMessage>(message);

                switch (gameMessage.Type)
                {
                    case "CREATE_ROOM":
                        server.CreateRoom(this);
                        break;
                    case "JOIN_ROOM":
                        server.JoinRoom(this, gameMessage.RoomId);
                        break;
                    case "READY":
                        server.SetPlayerReady(this, gameMessage.RoomId);
                        break;
                    case "SHIPS_PLACED":
                        var ships = JsonConvert.DeserializeObject<List<ShipPlacement>>(gameMessage.Data);
                        server.SetPlayerShips(this, gameMessage.RoomId, ships);
                        break;
                    case "SHOT":
                        var shot = JsonConvert.DeserializeObject<Shot>(gameMessage.Data);
                        server.ProcessShot(this, gameMessage.RoomId, shot);
                        break;
                }
            }
            catch { }
        }

        public void SendMessage(string message)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                stream.Write(buffer, 0, buffer.Length);
            }
            catch { }
        }
    }

    public class GameServer
    {
        private TcpListener listener;
        private Dictionary<string, GameRoom> rooms = new Dictionary<string, GameRoom>();

        public void Start(int port = 8888)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"Server started on port {port}");

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                var handler = new ClientHandler(client, this);
                handler.Start();
            }
        }

        public void CreateRoom(ClientHandler client)
        {
            string roomId = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
            var room = new GameRoom(roomId) { Player1 = client };
            rooms[roomId] = room;
            client.RoomId = roomId;

            var response = new GameMessage
            {
                Type = "ROOM_CREATED",
                Data = roomId,
                PlayerId = client.ClientId
            };

            client.SendMessage(JsonConvert.SerializeObject(response));
        }

        public void JoinRoom(ClientHandler client, string roomId)
        {
            if (rooms.TryGetValue(roomId, out var room) && !room.IsFull)
            {
                room.Player2 = client;
                client.RoomId = roomId;

                // Уведомляем обоих игроков
                var player1Message = new GameMessage
                {
                    Type = "PLAYER_JOINED",
                    Data = client.ClientId,
                    RoomId = roomId
                };

                var player2Message = new GameMessage
                {
                    Type = "ROOM_JOINED",
                    Data = roomId,
                    PlayerId = client.ClientId
                };

                room.Player1.SendMessage(JsonConvert.SerializeObject(player1Message));
                client.SendMessage(JsonConvert.SerializeObject(player2Message));
            }
        }

        public void SetPlayerReady(ClientHandler client, string roomId)
        {
            if (rooms.TryGetValue(roomId, out var room))
            {
                room.GameState.PlayersReady[client.ClientId] = true;

                if (room.GameState.GameStarted)
                {
                    room.CurrentPlayerTurn = room.Player1.ClientId;

                    var startMessage = new GameMessage
                    {
                        Type = "GAME_STARTED",
                        RoomId = roomId,
                        Data = room.CurrentPlayerTurn
                    };

                    BroadcastToRoom(roomId, startMessage);
                }
            }
        }

        public void SetPlayerShips(ClientHandler client, string roomId, List<ShipPlacement> ships)
        {
            if (rooms.TryGetValue(roomId, out var room))
            {
                room.GameState.PlayerShips[client.ClientId] = ships;
            }
        }

        public void ProcessShot(ClientHandler client, string roomId, Shot shot)
        {
            if (rooms.TryGetValue(roomId, out var room))
            {
                shot.PlayerId = client.ClientId;
                var opponent = client == room.Player1 ? room.Player2 : room.Player1;

                // Проверяем попадание
                if (room.GameState.PlayerShips.TryGetValue(opponent.ClientId, out var opponentShips))
                {
                    shot.IsHit = opponentShips.Any(ship =>
                        ship.Cells.Any(cell => cell.X == shot.X && cell.Y == shot.Y));

                    // Сохраняем выстрел
                    if (!room.GameState.PlayerShots.ContainsKey(client.ClientId))
                        room.GameState.PlayerShots[client.ClientId] = new List<Shot>();

                    room.GameState.PlayerShots[client.ClientId].Add(shot);

                    // Проверяем победителя
                    var winner = room.GameState.CheckWinner();
                    if (winner != null)
                    {
                        var winMessage = new GameMessage
                        {
                            Type = "GAME_OVER",
                            RoomId = roomId,
                            Data = winner
                        };

                        BroadcastToRoom(roomId, winMessage);
                    }
                    else
                    {
                        // Передаем ход
                        room.CurrentPlayerTurn = opponent.ClientId;

                        var shotResultMessage = new GameMessage
                        {
                            Type = "SHOT_RESULT",
                            RoomId = roomId,
                            Data = JsonConvert.SerializeObject(shot)
                        };

                        BroadcastToRoom(roomId, shotResultMessage);

                        var turnMessage = new GameMessage
                        {
                            Type = "TURN_CHANGED",
                            RoomId = roomId,
                            Data = room.CurrentPlayerTurn
                        };

                        BroadcastToRoom(roomId, turnMessage);
                    }
                }
            }
        }

        private void BroadcastToRoom(string roomId, GameMessage message)
        {
            if (rooms.TryGetValue(roomId, out var room))
            {
                var json = JsonConvert.SerializeObject(message);
                var clients = new List<ClientHandler> { room.Player1, room.Player2 };

                foreach (var client in clients.Where(c => c != null))
                {
                    client.SendMessage(json);
                }
            }
        }

        public void RemoveClient(ClientHandler client)
        {
            if (client.RoomId != null && rooms.TryGetValue(client.RoomId, out var room))
            {
                if (room.Player1 == client) room.Player1 = null;
                else if (room.Player2 == client) room.Player2 = null;

                // Удаляем комнату, если она пустая
                if (room.Player1 == null && room.Player2 == null)
                {
                    rooms.Remove(client.RoomId);
                }
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var server = new GameServer();
            server.Start();
        }
    }
}