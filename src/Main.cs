using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Collections;
using System.Reflection;

namespace OpenSMO
{
  public class MainClass
  {
    public TcpListener tcpListener;
    public TcpListener tcpListenerRTS;
    public int FPS = 120;

    public List<User> Users = new List<User>();
    public List<Room> Rooms = new List<Room>();

    public byte ServerOffset = 128;
    public byte ServerVersion = 128;
    public byte ServerMaxPlayers = 255;

    public Config ServerConfig;
    public Scripting Scripting;

    public static int Build = 8;
    public static MainClass Instance;
    public static DateTime StartTime;

    void ShowHelp()
    {
      Console.WriteLine("Usage is: OpenSMO [options]");
      Console.WriteLine("Options:");
      Console.WriteLine("  -h            : Show this help");
      Console.WriteLine("  -v            : Show current version");
      Console.WriteLine("  -c <filename> : Load a specific config file");
    }

    public MainClass(string[] args)
    {
      Instance = this;
      StartTime = DateTime.Now;

      string argConfigFile = "Config.ini";

      try {
        for (int i = 0; i < args.Length; i++) {
          switch (args[i]) {
            case "--help":
            case "-h":
            case "-?":
            default:
              this.ShowHelp();
              return;

            case "--version":
            case "-v":
              Console.WriteLine("OpenSMO build " + Build);
              return;

            case "--config":
            case "-c":
              argConfigFile = args[++i];
              break;
          }
        }
      } catch {
        this.ShowHelp();
      }

      ServerConfig = new Config(argConfigFile);

      Console.Title = ServerConfig.Get("Server_Name");

      AddLog("Server starting at " + StartTime);

      if (bool.Parse(ServerConfig.Get("Server_HigherPriority"))) {
        AddLog("Setting priority to above normal");
        Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
      }

      FPS = int.Parse(ServerConfig.Get("Server_FPS"));

      // Get optional advanced settings
      if (ServerConfig.Contains("Server_Offset")) ServerOffset = (byte)int.Parse(ServerConfig.Get("Server_Offset"));
      if (ServerConfig.Contains("Server_Version")) ServerVersion = (byte)int.Parse(ServerConfig.Get("Server_Version"));
      if (ServerConfig.Contains("Server_MaxPlayers")) ServerMaxPlayers = (byte)int.Parse(ServerConfig.Get("Server_MaxPlayers"));

      MySql.Host = ServerConfig.Get("MySql_Host");
      MySql.User = ServerConfig.Get("MySql_User");
      MySql.Password = ServerConfig.Get("MySql_Password");
      MySql.Database = ServerConfig.Get("MySql_Database");


      Hashtable[] fixedRooms = MySql.Query("SELECT * FROM fixedrooms;");

      if (fixedRooms == null) {
        AddLog("It appears there's no \"fixedrooms\" table, creating one now.");
        MySql.Query(@"CREATE TABLE ""main"".""fixedrooms"" (
          ""ID""  INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
          ""Name""  TEXT(255) NOT NULL,
          ""Description""  TEXT(255),
          ""Password""  TEXT(255),
          ""Free""  INTEGER,
          ""MOTD""  TEXT(255),
          ""Operators""  TEXT(255));");
      } else {
        foreach (Hashtable room in fixedRooms) {
          Room newRoom = new Room(this, null);
          newRoom.Fixed = true;
          newRoom.Name = room["Name"].ToString();
          newRoom.Description = room["Description"].ToString();
          newRoom.Password = room["Password"].ToString();
          newRoom.Free = room["Free"].ToString() == "1";
          newRoom.FixedMotd = room["MOTD"].ToString();

          string[] strOps = room["Operators"].ToString().Split(',');
          List<int> ops = new List<int>();
          foreach (string op in strOps) {
            if (op == "") {
              continue;
            }

            int opID = 0;
            if (int.TryParse(op, out opID)) {
              ops.Add(opID);
            } else {
              AddLog("Invalid op ID '" + op + "'");
            }
          }
          newRoom.FixedOperators = ops.ToArray();
          Rooms.Add(newRoom);

          AddLog("Added fixed room '" + newRoom.Name + "'");
        }
      }

      ReloadScripts();

      tcpListener = new TcpListener(IPAddress.Parse(ServerConfig.Get("Server_IP")), int.Parse(ServerConfig.Get("Server_Port")));
      tcpListener.Start();

      AddLog("Server started on port " + ServerConfig.Get("Server_Port"));

      new Thread(new ThreadStart(UserThread)).Start();

      if (bool.Parse(ServerConfig.Get("RTS_Enabled"))) {
        tcpListenerRTS = new TcpListener(IPAddress.Parse(ServerConfig.Get("RTS_IP")), int.Parse(ServerConfig.Get("RTS_Port")));
        tcpListenerRTS.Start();

        AddLog("RTS server started on port " + ServerConfig.Get("RTS_Port"));

        new Thread(new ThreadStart(RTSThread)).Start();
      }

      AddLog("Server running.");

      while (true) {
        TcpClient newTcpClient = tcpListener.AcceptTcpClient();

        string IP = newTcpClient.Client.RemoteEndPoint.ToString().Split(':')[0];
        if (Data.IsBanned(IP)) {
          if (bool.Parse(ServerConfig.Get("Game_ShadowBan"))) {
            AddLog("Shadowbanned client connected: " + IP, true);

            User newUser = new User(this, newTcpClient);
            newUser.ShadowBanned = true;
            Users.Add(newUser);
          } else {
            AddLog("Banned client kicked: " + IP, true);
            newTcpClient.Close();
          }
        } else {
          AddLog("Client connected: " + IP);

          User newUser = new User(this, newTcpClient);
          Users.Add(newUser);
        }
      }
    }

    public void ReloadScripts()
    {
      Scripting = new Scripting();

      Scripting.Scope.SetVariable("main", this);
      Scripting.Scope.SetVariable("config", ServerConfig);

      Scripting.Start();
    }

    public long Epoch()
    {
      return (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000;
    }

    public void UserThread()
    {
      while (true) {
        try {
          for (int i = 0; i < Scripting.UpdateHooks.Count; i++) {
            Scripting.UpdateHooks[i]();
          }
        } catch (Exception ex) { Scripting.HandleError(ex); }

        for (int i = 0; i < Users.Count; i++)
          Users[i].Update();
        for (int i = 0; i < Rooms.Count; i++)
          Rooms[i].Update();

        Thread.Sleep(1000 / FPS);
      }
    }

    public static Random rnd = new Random();
    public static string RandomString(int len, string chars = "abcdefghijklmnopqrstuvwxyz0123456789")
    {
      string ret = "";
      for (int i = 0; i < len; i++) {
        string a = chars[rnd.Next(chars.Length)].ToString();
        if (rnd.Next(2) == 0)
          ret += a.ToUpper();
        else
          ret += a;
      }
      return ret;
    }

    public string JsonSafe(string str)
    {
      return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }

    public void RTSThread()
    {
      while (true) {
        TcpClient newClient = tcpListenerRTS.AcceptTcpClient();

        new Thread(new ThreadStart(delegate
        {
          TcpClient newTcpClient = newClient;
          string IP = newTcpClient.Client.RemoteEndPoint.ToString().Split(':')[0];

          NetworkStream stream = newTcpClient.GetStream();
          StreamReader reader = new StreamReader(stream);
          StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };

          try {
            string line = reader.ReadLine();

            while (reader.ReadLine() != "") { }

            if (line != null) {
              string[] requestParts = line.Split(' ')[1].Substring(1).Split(new char[] { '?' }, 2);
              string request = requestParts[0];
              string data = requestParts.Length == 2 ? Uri.UnescapeDataString(requestParts[1]).Replace('+', ' ') : "";
              string[] parse = request.Split('/');

              string roomID = "";
              Room r = null;

              string responseBuffer = "";
              switch (parse[0]) {
                case "l":
                  if (IP != ServerConfig.Get("RTS_Trusted")) {
                    responseBuffer = "[]";
                    break;
                  }

                  responseBuffer = "[";
                  foreach (Room room in Rooms) {
                    if (!room.Secret && (room.Owner != null && !room.Owner.ShadowBanned)) {
                      responseBuffer += "[";
                      responseBuffer += "\"" + room.ID + "\",";
                      responseBuffer += "\"" + JsonSafe(room.Name) + "\",";
                      responseBuffer += "\"" + JsonSafe(room.Description) + "\",";
                      responseBuffer += "\"" + JsonSafe(room.Owner.User_Name) + "\",";
                      responseBuffer += room.Users.Count + ",";
                      responseBuffer += "\"" + room.Status.ToString() + "\",";
                      responseBuffer += "\"" + JsonSafe(room.CurrentSong.Name) + "\",";
                      responseBuffer += "\"" + JsonSafe(room.CurrentSong.Artist) + "\"";
                      responseBuffer += "]";

                      if (Rooms.Last() != room)
                        responseBuffer += ",";
                    }
                  }
                  responseBuffer += "]";
                  break;

                case "g":
                  roomID = parse[1];
                  r = null;
                  foreach (Room room in Rooms) {
                    if (room.ID == roomID) {
                      r = room;
                      break;
                    }
                  }

                  if (r == null || r.Secret) {
                    responseBuffer = "[]";
                  } else {
                    User[] usersOrig = r.Users.ToArray();
                    if (usersOrig.Length == 0) {
                      responseBuffer = "[]";
                    } else {
                      User[] users = (from user in usersOrig orderby user.SMOScore descending select user).ToArray();
                      responseBuffer += "[[\"" + JsonSafe(r.Name) + "\",";
                      responseBuffer += "\"" + JsonSafe(r.Description) + "\",";
                      responseBuffer += "\"" + JsonSafe(r.CurrentSong.Artist + " - " + r.CurrentSong.Name) + "\",";
                      if (r.CurrentSong.Time == 0)
                        responseBuffer += "false,";
                      else
                        responseBuffer += "\"" + (int)Math.Min(100, Math.Floor(100d / r.CurrentSong.Time * usersOrig[0].SongTime.ElapsedMilliseconds / 1000d)) + "%\",";
                      responseBuffer += "\"" + JsonSafe(r.ChatBuffer) + "\"";
                      responseBuffer += "],";
                      foreach (User user in users) {
                        responseBuffer += "[";
                        responseBuffer += user.User_ID + ",";
                        responseBuffer += "\"" + JsonSafe(user.User_Name) + "\",";
                        responseBuffer += "\"" + user.Combo + " / " + user.MaxCombo + "\",";
                        responseBuffer += user.SMOScore + ",";
                        responseBuffer += "\"" + user.Grade + "\",";
                        responseBuffer += "\"" + user.GameDifficulty + "\",";
                        responseBuffer += "\"" + JsonSafe(user.GamePlayerSettings) + "\"";
                        responseBuffer += "],";
                      }
                      responseBuffer += "]";
                    }
                  }
                  break;

                case "c":
                  if (IP != ServerConfig.Get("RTS_Trusted")) {
                    responseBuffer = "[]";
                    break;
                  }

                  roomID = parse[1];

                  Hashtable[] userRes =MySql.Query("SELECT * FROM users WHERE Username='" +MySql.AddSlashes(parse[2]) + "'");
                  if (userRes.Length != 1) {
                    break;
                  }

                  Hashtable u = userRes[0];

                  r = null;
                  foreach (Room room in Rooms) {
                    if (room.ID == roomID) {
                      r = room;
                      break;
                    }
                  }

                  if (r != null && !r.Secret) {
                    string strName = u["Username"].ToString();

                    for (int i = 0; i < Scripting.WebFormatHooks.Count; i++) {
                      strName = Scripting.WebFormatHooks[i](u, strName);
                    }

                    SendChatAll(strName + ": " + data, r);
                  }

                  responseBuffer = "OK";
                  break;
              }

              writer.WriteLine("HTTP/1.1 200 OK");
              writer.WriteLine("Content-Type: text/plain");
              writer.WriteLine("access-control-allow-origin: *");
              writer.WriteLine("access-control-allow-credentials: true");
              writer.WriteLine("Content-Length: " + responseBuffer.Length);
              writer.WriteLine("Connection: close");
              writer.WriteLine();
              writer.Write(responseBuffer);
            }
          } catch (Exception ex) {
            AddLog("RTS request encountered '" + ex.GetType().Name + "' from " + IP, true);
          }

          stream.Close();
          stream.Dispose();
        })).Start();
      }
    }

    public static string Spaces(string input, int spaceCount)
    {
      string ret = "";
      for (int i = 0; i < spaceCount - input.Length; i++)
        ret += ' ';
      return ret + input;
    }

    public static void AddLog(string Str, bool Bad = false)
    {
      if (Bad) Console.ForegroundColor = ConsoleColor.Red;
      string line = "[" + Spaces(((DateTime.Now - StartTime).TotalMilliseconds / 1000d).ToString("0.000000").Replace(',', '.'), 14) + "] " + Str;
      Console.WriteLine(line);
      if (Bad) Console.ForegroundColor = ConsoleColor.Gray;

      string logFilename = Instance.ServerConfig.Get("Server_LogFile");
      if (logFilename != "") {
        StreamWriter writer;
        if (File.Exists(logFilename))
          writer = File.AppendText(logFilename);
        else
          writer = new StreamWriter(File.Create(logFilename));

        writer.WriteLine(line);
        writer.Close();
      }
    }

    public void SendChatAll(string Message)
    {
      foreach (User user in Users)
        user.SendChatMessage(Message);
    }

    public void SendChatAll(string Message, Room room)
    {
      if (room != null)
        room.AddChatBuffer(Message);

      for (int i = 0; i < Users.Count; i++) {
        User user = Users[i];
        if (user.CurrentRoom == room)
          user.SendChatMessage(Message);
      }
    }

    public void SendChatAll(string Message, Room room, User exception)
    {
      foreach (User user in Users) {
        if (user.CurrentRoom == room && user != exception)
          user.SendChatMessage(Message);
      }
    }

    public static void Main(string[] args)
    {
      new MainClass(args);
    }

    public static string MD5(string input)
    {
      byte[] hashBytes = System.Security.Cryptography.MD5.Create().ComputeHash(Encoding.ASCII.GetBytes(input));

      StringBuilder sb = new StringBuilder();
      for (int i = 0; i < hashBytes.Length; i++)
        sb.Append(hashBytes[i].ToString("x2"));

      return sb.ToString().ToUpper();
    }
  }
}
