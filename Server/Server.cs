﻿using System;
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
        public Dictionary<string, bool> ShipsPlaced { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, List<ShipPlacement>> PlayerShips { get; set; } = new Dictionary<string, List<ShipPlacement>>();
        public Dictionary<string, List<Shot>> PlayerShots { get; set; } = new Dictionary<string, List<Shot>>();
        public bool GameStarted => PlayersReady.Count == 2 && PlayersReady.All(p => p.Value) &&
                                   ShipsPlaced.Count == 2 && ShipsPlaced.All(p => p.Value);

        public string CheckWinner()
        {
            foreach (var player in PlayerShips.Keys)
            {
                var opponent = PlayerShips.Keys.First(p => p != player);
                var playerShips = PlayerShips[player];
                var opponentShots = PlayerShots.ContainsKey(opponent) ? PlayerShots[opponent] : new List<Shot>();

                bool allShipsSunk = playerShips.All(ship =>
                    ship.Cells.All(cell =>
                        opponentShots.Any(shot => shot.X == cell.X && shot.Y == shot.Y)));

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
        private string _clientIp;

        public ClientHandler(TcpClient client, GameServer server)
        {
            this.client = client;
            this.server = server;
            this.stream = client.GetStream();
            ClientId = Guid.NewGuid().ToString();

            // Получаем IP клиента для логирования
            _clientIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Игрок подключился с IP: {_clientIp}");
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
                catch
                {
                    server.RemoveClient(this);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Игрок отключился (IP: {_clientIp})");
                }
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
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Игрок {ClientId.Substring(0, 8)} создает комнату");
                        server.CreateRoom(this);
                        break;
                    case "JOIN_ROOM":
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Игрок {ClientId.Substring(0, 8)} присоединяется к комнате {gameMessage.RoomId}");
                        server.JoinRoom(this, gameMessage.RoomId);
                        break;
                    case "READY":
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Игрок {ClientId.Substring(0, 8)} нажал Готов в комнате {gameMessage.RoomId}");
                        server.SetPlayerReady(this, gameMessage.RoomId);
                        break;
                    case "SHIPS_PLACED":
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Игрок {ClientId.Substring(0, 8)} расставил корабли в комнате {gameMessage.RoomId}");
                        var ships = JsonConvert.DeserializeObject<List<ShipPlacement>>(gameMessage.Data);
                        server.SetPlayerShips(this, gameMessage.RoomId, ships);
                        break;
                    case "SHOT":
                        var shot = JsonConvert.DeserializeObject<Shot>(gameMessage.Data);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Игрок {ClientId.Substring(0, 8)} делает выстрел ({shot.X},{shot.Y}) в комнате {gameMessage.RoomId}");
                        server.ProcessShot(this, gameMessage.RoomId, shot);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка обработки сообщения от {_clientIp}: {ex.Message}");
            }
        }

        public void SendMessage(string message)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                stream.Write(buffer, 0, buffer.Length);
            }
            catch
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка отправки сообщения игроку {ClientId.Substring(0, 8)}");
            }
        }
    }

    public class GameServer
    {
        private TcpListener listener;
        private Dictionary<string, GameRoom> rooms = new Dictionary<string, GameRoom>();
        private int _connectedClients = 0;

        public void Start(int port = 8888)
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();

                // Получаем сетевые IP адреса
                var localIps = GetLocalIPAddresses();
                string localhost = "127.0.0.1";

                Console.WriteLine("===============================================");
                Console.WriteLine($"СЕРВЕР ЗАПУЩЕН УСПЕШНО!");
                Console.WriteLine("===============================================");
                Console.WriteLine($"Порт: {port}");
                Console.WriteLine($"Локальный IP (для подключения на этом ПК):");
                Console.WriteLine($"  → {localhost}:{port}");

                if (localIps.Any())
                {
                    Console.WriteLine($"\nСетевые IP (для подключения с других устройств в сети):");
                    foreach (var ip in localIps)
                    {
                        Console.WriteLine($"  → {ip}:{port}");
                    }
                }
                else
                {
                    Console.WriteLine($"\n⚠ Сетевые IP не найдены. Возможно, нет активного сетевого подключения.");
                }

                Console.WriteLine("===============================================");
                Console.WriteLine($"Ожидание подключений...");
                Console.WriteLine($"Нажмите Ctrl+C для остановки сервера");
                Console.WriteLine("===============================================\n");

                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    _connectedClients++;
                    var handler = new ClientHandler(client, this);
                    handler.Start();

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Подключен новый игрок! Всего подключений: {_connectedClients}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка запуска сервера: {ex.Message}");
            }
        }

        private List<string> GetLocalIPAddresses()
        {
            var localIps = new List<string>();
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    // Берем только IPv4 адреса, которые не являются loopback (127.0.0.1)
                    if (ip.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ip))
                    {
                        localIps.Add(ip.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка получения сетевых IP: {ex.Message}");
            }
            return localIps;
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

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Создана комната: {roomId}");
        }

        public void JoinRoom(ClientHandler client, string roomId)
        {
            if (rooms.TryGetValue(roomId, out var room) && !room.IsFull)
            {
                room.Player2 = client;
                client.RoomId = roomId;

                // Уведомляем обоих игроков о подключении
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

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Игрок присоединился к комнате {roomId}. Комната заполнена!");

                // ОТПРАВЛЯЕМ НОВОМУ ИГРОКУ СОСТОЯНИЕ КОМНАТЫ
                SendRoomStateToPlayer(client, roomId, room);
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Не удалось присоединиться к комнате {roomId} (не найдена или заполнена)");
            }
        }

        private void SendRoomStateToPlayer(ClientHandler client, string roomId, GameRoom room)
        {
            try
            {
                // Отправляем информацию о готовности другого игрока
                foreach (var playerReady in room.GameState.PlayersReady)
                {
                    if (playerReady.Key != client.ClientId && playerReady.Value)
                    {
                        var readyMessage = new GameMessage
                        {
                            Type = "PLAYER_READY",
                            RoomId = roomId,
                            PlayerId = playerReady.Key,
                            Data = playerReady.Key
                        };

                        client.SendMessage(JsonConvert.SerializeObject(readyMessage));
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Отправлено состояние готовности игрока {playerReady.Key.Substring(0, 8)} новому игроку");
                    }
                }

                // Отправляем информацию о расставленных кораблях другого игрока
                foreach (var shipsPlaced in room.GameState.ShipsPlaced)
                {
                    if (shipsPlaced.Key != client.ClientId && shipsPlaced.Value)
                    {
                        var shipsMessage = new GameMessage
                        {
                            Type = "SHIPS_PLACED_NOTIFY",
                            RoomId = roomId,
                            PlayerId = shipsPlaced.Key,
                            Data = shipsPlaced.Key
                        };

                        client.SendMessage(JsonConvert.SerializeObject(shipsMessage));
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Отправлено состояние кораблей игрока {shipsPlaced.Key.Substring(0, 8)} новому игроку");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка отправки состояния комнаты: {ex.Message}");
            }
        }

        public void SetPlayerReady(ClientHandler client, string roomId)
        {
            if (rooms.TryGetValue(roomId, out var room))
            {
                room.GameState.PlayersReady[client.ClientId] = true;

                // Отправляем уведомление обоим игрокам о готовности
                var readyMessage = new GameMessage
                {
                    Type = "PLAYER_READY",
                    RoomId = roomId,
                    PlayerId = client.ClientId,
                    Data = client.ClientId // ID игрока, который готов
                };

                BroadcastToRoom(roomId, readyMessage);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Игрок {client.ClientId.Substring(0, 8)} готов в комнате {roomId}. Уведомление отправлено.");

                // Проверяем, можно ли начать игру
                CheckAndStartGame(roomId, room);
            }
        }

        public void SetPlayerShips(ClientHandler client, string roomId, List<ShipPlacement> ships)
        {
            if (rooms.TryGetValue(roomId, out var room))
            {
                room.GameState.PlayerShips[client.ClientId] = ships;
                room.GameState.ShipsPlaced[client.ClientId] = true;

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Игрок {client.ClientId.Substring(0, 8)} расставил корабли в комнате {roomId}");

                // Отправляем уведомление обоим игрокам о расстановке кораблей
                var shipsMessage = new GameMessage
                {
                    Type = "SHIPS_PLACED_NOTIFY",
                    RoomId = roomId,
                    PlayerId = client.ClientId,
                    Data = client.ClientId
                };

                BroadcastToRoom(roomId, shipsMessage);

                // Проверяем, можно ли начать игру
                CheckAndStartGame(roomId, room);
            }
        }

        private void CheckAndStartGame(string roomId, GameRoom room)
        {
            var readyCount = room.GameState.PlayersReady.Count(p => p.Value);
            var shipsCount = room.GameState.ShipsPlaced.Count(p => p.Value);

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Статус комнаты {roomId}: {readyCount}/2 готовы, {shipsCount}/2 расставили корабли");

            if (room.GameState.GameStarted)
            {
                // ОБЯЗАТЕЛЬНО отправляем статус готовности обоим игрокам перед началом игры
                foreach (var playerId in room.GameState.PlayersReady.Keys)
                {
                    if (room.GameState.PlayersReady[playerId])
                    {
                        var playerReadyMessage = new GameMessage
                        {
                            Type = "PLAYER_READY",
                            RoomId = roomId,
                            PlayerId = playerId,
                            Data = playerId
                        };
                        BroadcastToRoom(roomId, playerReadyMessage);
                    }
                }

                foreach (var playerId in room.GameState.ShipsPlaced.Keys)
                {
                    if (room.GameState.ShipsPlaced[playerId])
                    {
                        var shipsPlacedMessage = new GameMessage
                        {
                            Type = "SHIPS_PLACED_NOTIFY",
                            RoomId = roomId,
                            PlayerId = playerId,
                            Data = playerId
                        };
                        BroadcastToRoom(roomId, shipsPlacedMessage);
                    }
                }

                // Определяем, кто ходит первым
                room.CurrentPlayerTurn = room.Player1.ClientId;

                var startMessage = new GameMessage
                {
                    Type = "GAME_STARTED",
                    RoomId = roomId,
                    Data = room.CurrentPlayerTurn
                };

                BroadcastToRoom(roomId, startMessage);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Игра началась в комнате {roomId}! Первый ход у игрока {room.CurrentPlayerTurn.Substring(0, 8)}");
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

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Выстрел в комнате {roomId}: {shot.X},{shot.Y} - {(shot.IsHit ? "ПОПАДАНИЕ!" : "промах")}");

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

                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ИГРА ОКОНЧЕНА в комнате {roomId}! Победитель: {winner.Substring(0, 8)}");
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

                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ход передан игроку {room.CurrentPlayerTurn.Substring(0, 8)} в комнате {roomId}");
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

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Игрок вышел из комнаты {client.RoomId}");

                // Уведомляем оставшегося игрока об отключении
                var remainingPlayer = room.Player1 ?? room.Player2;
                if (remainingPlayer != null)
                {
                    var disconnectMessage = new GameMessage
                    {
                        Type = "PLAYER_DISCONNECTED",
                        RoomId = client.RoomId,
                        Data = client.ClientId
                    };
                    remainingPlayer.SendMessage(JsonConvert.SerializeObject(disconnectMessage));
                }

                // Удаляем комнату, если она пустая
                if (room.Player1 == null && room.Player2 == null)
                {
                    rooms.Remove(client.RoomId);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Комната {client.RoomId} удалена (пустая)");
                }
            }

            _connectedClients--;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Подключенных игроков: {_connectedClients}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Sea Battle Game Server";

            Console.WriteLine("Запуск сервера для игры 'Морской бой'...");
            Console.WriteLine("Версия: 1.0");
            Console.WriteLine("Авторы: DKrey и Yanl1n, 2025\n");

            var server = new GameServer();
            server.Start();
        }
    }
}