﻿using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Sea_battle_WPF.Core.Services
{
    public class NetworkService
    {
        private TcpClient client;
        private NetworkStream stream;
        private Thread receiveThread;
        public bool IsConnected => client?.Connected ?? false;
        public string PlayerId { get; private set; }
        public string RoomId { get; private set; }
        public string CurrentTurnPlayerId { get; private set; }
        public bool IsMyTurn => CurrentTurnPlayerId == PlayerId;

        // Существующие события
        public event Action<string> OnRoomCreated;
        public event Action<string> OnRoomJoined;
        public event Action<string> OnPlayerJoined;
        public event Action OnGameStarted;
        public event Action<string> OnTurnChanged;
        public event Action<ShotResult> OnShotResult;
        public event Action<string> OnGameOver;
        public event Action<string> OnError;

        // НОВЫЕ события
        public event Action<string> OnPlayerReady;
        public event Action<string> OnPlayerDisconnected;
        public event Action<string> OnShipsPlacedNotify; // Новое событие

        public async Task<bool> Connect(string serverIp = "127.0.0.1", int port = 8888)
        {
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(serverIp, port);
                stream = client.GetStream();

                StartReceiving();
                return true;
            }
            catch
            {
                OnError?.Invoke("Ошибка подключения");
                return false;
            }
        }

        public void Disconnect()
        {
            receiveThread?.Abort();
            stream?.Close();
            client?.Close();
        }

        private void StartReceiving()
        {
            receiveThread = new Thread(() =>
            {
                byte[] buffer = new byte[4096];

                while (client.Connected)
                {
                    try
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            ProcessMessage(message);
                        }
                    }
                    catch { break; }
                }
            });

            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        private void ProcessMessage(string message)
        {
            try
            {
                var gameMessage = JsonConvert.DeserializeObject<GameMessage>(message);

                switch (gameMessage.Type)
                {
                    case "ROOM_CREATED":
                        RoomId = gameMessage.Data;
                        PlayerId = gameMessage.PlayerId;
                        OnRoomCreated?.Invoke(RoomId);
                        break;

                    case "ROOM_JOINED":
                        RoomId = gameMessage.Data;
                        PlayerId = gameMessage.PlayerId;
                        OnRoomJoined?.Invoke(RoomId);
                        break;

                    case "PLAYER_JOINED":
                        OnPlayerJoined?.Invoke(gameMessage.Data);
                        break;

                    case "PLAYER_READY":
                        // НОВОЕ: Обработка готовности игрока
                        OnPlayerReady?.Invoke(gameMessage.Data);
                        break;

                    case "SHIPS_PLACED_NOTIFY":
                        // НОВОЕ: Обработка уведомления о расстановке кораблей
                        OnShipsPlacedNotify?.Invoke(gameMessage.Data);
                        break;

                    case "PLAYER_DISCONNECTED":
                        // НОВОЕ: Обработка отключения игрока
                        OnPlayerDisconnected?.Invoke(gameMessage.Data);
                        break;

                    case "GAME_STARTED":
                        CurrentTurnPlayerId = gameMessage.Data;
                        OnGameStarted?.Invoke();
                        break;

                    case "TURN_CHANGED":
                        CurrentTurnPlayerId = gameMessage.Data;
                        OnTurnChanged?.Invoke(gameMessage.Data);
                        break;

                    case "SHOT_RESULT":
                        var shot = JsonConvert.DeserializeObject<Shot>(gameMessage.Data);
                        OnShotResult?.Invoke(new ShotResult
                        {
                            X = shot.X,
                            Y = shot.Y,
                            IsHit = shot.IsHit,
                            PlayerId = shot.PlayerId
                        });
                        break;

                    case "GAME_OVER":
                        OnGameOver?.Invoke(gameMessage.Data);
                        break;
                }
            }
            catch { }
        }

        private void SendMessage(GameMessage message)
        {
            try
            {
                message.PlayerId = PlayerId;
                string json = JsonConvert.SerializeObject(message);
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                stream.Write(buffer, 0, buffer.Length);
            }
            catch { }
        }

        public void CreateRoom()
        {
            var message = new GameMessage { Type = "CREATE_ROOM" };
            SendMessage(message);
        }

        public void JoinRoom(string roomId)
        {
            var message = new GameMessage { Type = "JOIN_ROOM", RoomId = roomId };
            SendMessage(message);
        }

        public void SetReady()
        {
            var message = new GameMessage { Type = "READY", RoomId = RoomId };
            SendMessage(message);
        }

        public void SendShips(List<ShipPlacement> ships)
        {
            var message = new GameMessage
            {
                Type = "SHIPS_PLACED",
                RoomId = RoomId,
                Data = JsonConvert.SerializeObject(ships)
            };
            SendMessage(message);
        }

        public void SendShot(int x, int y)
        {
            var shot = new Shot { X = x, Y = y };
            var message = new GameMessage
            {
                Type = "SHOT",
                RoomId = RoomId,
                Data = JsonConvert.SerializeObject(shot)
            };
            SendMessage(message);
        }
    }

    public class GameMessage
    {
        public string Type { get; set; }
        public string Data { get; set; }
        public string PlayerId { get; set; }
        public string RoomId { get; set; }
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

    public class ShotResult
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsHit { get; set; }
        public string PlayerId { get; set; }
    }
}