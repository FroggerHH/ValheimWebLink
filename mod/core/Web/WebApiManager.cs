﻿using System.Reflection;
using BepInEx;
using ValheimWebLink.Web.Controllers;

namespace ValheimWebLink.Web;

public static class WebApiManager
{
    public static HashSet<IController> Controllers { get; private set; } = [];
    private static HttpListener _listener;
    public static CancellationTokenSource _cancellationTokenSource { get; private set; }

    private static AuthData _authData;

    public static void ReloadPort()
    {
        var port = SettingsManager.instance.port;
        Debug($"Reloading Web API server on port {port}...", ConsoleColor.DarkGreen);
        _listener?.Stop();
        _listener?.Prefixes.Clear();
        _listener?.Prefixes.Add($"http://*:{port}/");
        _listener?.Start();
        Debug($"Web API server on port {port} reloaded", ConsoleColor.Green);
    }

    public static void Init()
    {
        var port = SettingsManager.instance.port;
        Debug($"Starting Web API server on port {port}...");
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{port}/");
        //TODO: Add https
        _listener.Start();

        _cancellationTokenSource = new CancellationTokenSource();

        FindControllers();

        var authData = new AuthData("root", "root");
        try
        {
            _authData = JSON.ToObject<AuthData>(File.ReadAllText("auth.json"));
            if (_authData == null)
            {
                File.WriteAllText("auth.json", JSON.ToNiceJSON(authData));
                Debug("Your login and password are default. You MUST edit it before running the server",
                    ConsoleColor.DarkMagenta);
            } else
            {
                if (_authData.login == "root" || _authData.password == "root")
                {
                    Debug("Your login and password are default. You MUST edit it before running the server",
                        ConsoleColor.DarkMagenta);
                }

                if (!_authData.login.IsGood())
                {
                    Debug("Your login is in invalid format. Change it. Aborting...", ConsoleColor.DarkMagenta);
                    Stop();
                }

                if (!_authData.password.IsGood())
                {
                    Debug("Your password is in invalid format. Change it. Aborting...", ConsoleColor.DarkMagenta);
                    Stop();
                }
            }
        }
        catch (Exception e)
        {
            File.WriteAllText("auth.json", JSON.ToNiceJSON(authData));
            Debug("Your auth.json file is corrupted. "
                  + "You MUST edit it before running the server.\n"
                  + $"Exeption: {e.GetType().Name} {e.Message}",
                ConsoleColor.DarkMagenta);

            _authData = authData;
        }

        SetupAuthDataWatcher();

        var listenerThread = new Thread(() => Listen(_cancellationTokenSource.Token));
        listenerThread.IsBackground = true;
        listenerThread.Start();
    }

    private static void SetupAuthDataWatcher()
    {
        if (!File.Exists("auth.json"))
        {
            Debug("This can not happen");
            return;
        }

        FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(Paths.GameRootPath, "auth.json");
        fileSystemWatcher.Changed += (_, _) =>
        {
            try
            {
                var authData = JSON.ToObject<AuthData>(File.ReadAllText("auth.json"));
                if (authData == null)
                {
                    Debug("Your login and password are default or file is corrupted. "
                          + "You MUST edit it before running the server.",
                        ConsoleColor.DarkMagenta);
                    return;
                }

                _authData = authData;
            }
            catch (Exception exception)
            {
                Debug("Your login and password are default or file is corrupted. "
                      + "You MUST edit it before running the server.\n"
                      + $"Exeption: {exception.GetType().Name} {exception.Message}",
                    ConsoleColor.DarkMagenta);
            }
        };
        fileSystemWatcher.IncludeSubdirectories = true;
        fileSystemWatcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        fileSystemWatcher.EnableRaisingEvents = true;
    }

    private static void FindControllers()
    {
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (Attribute.IsDefined(type, typeof(ControllerAttribute)))
            {
                var instance = Activator.CreateInstance(type);
                var controller = (IController)instance;
                if (Controllers.ToList()
                    .Exists(x => x.HttpMethod == controller.HttpMethod && x.Route == controller.Route))
                {
                    Debug($"Controller {type.Name} already exists. Skipping...", ConsoleColor.Yellow);
                    continue;
                }

                Controllers.Add(instance as IController);
            }
        }
    }

    public static void Stop()
    {
        Debug("Stopping server...");
        _cancellationTokenSource?.Cancel();
        _listener?.Stop();
    }

    private static async void Listen(CancellationToken cancellationToken)
    {
        try
        {
            _listener.Start();
            Debug($"\n"
                  + $"Some requests required authentication\n"
                  + $"Your login and password are stored in the file 'auth.json'\n"
                  + $"You MUST edit it before running the server\n"
                  + $"\n\n");

            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync();

                _ = Task.Run(() => HandleRequest(context, cancellationToken), cancellationToken);
            }
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 995)
        {
            // Игнорировать HttpListenerException с кодом ошибки 995 (Операция отменена)
            DebugWarning($"Exception(995): {ex.Message}");
        }
        catch (OperationCanceledException ex)
        {
            // Обработка отмены операции
            DebugWarning($"Operation was canceled. {ex.Message}");
        }
        catch (Exception ex)
        {
            // Обработка других исключений
            DebugError($"Unhandled exception:\n{ex}");
        }
        finally
        {
            _listener.Close();
            Debug("Stopped listening for requests");
        }
    }

    private static async Task HandleRequest(HttpListenerContext context, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            DebugWarning("HandleRequest: Request was cancelled");
            return;
        }

        var request = context.Request;
        var url = request.Url;
        var httpMethod = request.HttpMethod;
        var absolutePath = url.AbsolutePath;
        var response = context.Response;

        foreach (var controller in Controllers)
            if (httpMethod == controller.HttpMethod && absolutePath == controller.Route)
            {
                Debug($"Got {absolutePath} request, url: {url}");
                try
                {
                    await controller.HandleRequest(request, response, IsAuthed(request), GetQueryParameters(request));
                }
                catch (Exception e)
                {
                    DebugError($"Request handler threw an exception: {e}");
                    SendResponce(response, 500, "application/json", new WebExceptionJSON(e));
                }

                response.Close();
                return;
            }

        if (Controllers.ToList().Exists(x => x.Route == absolutePath))
        {
            SendResponce(response, MethodNotAllowed);
            return;
        }

        SendResponce(response, BadRequest);
        response.Close();
    }

    public static bool IsAuthed(HttpListenerRequest request)
    {
        if (_authData == null)
        {
            Debug("AuthData is null", ConsoleColor.Red);
            return false;
        }

        var authHeader = request.Headers.Get("Authorization");
        if (!authHeader.IsGood()) return false;
        var authData = ExtractUsernameAndPassword(authHeader);

        return authData.login == _authData.login && authData.password == _authData.password;
    }

    private static AuthData ExtractUsernameAndPassword(string authHeader)
    {
        string encodedUsernamePassword = authHeader.Substring("Basic ".Length).Trim();
        Encoding encoding = Encoding.GetEncoding("iso-8859-1");
        string usernamePassword = encoding.GetString(Convert.FromBase64String(encodedUsernamePassword));

        int separatorIndex = usernamePassword.IndexOf(':');
        string username = usernamePassword.Substring(0, separatorIndex);
        string password = usernamePassword.Substring(separatorIndex + 1);

        return new(username, password);
    }

    private static Dictionary<string, string> GetQueryParameters(HttpListenerRequest request)
    {
        Dictionary<string, string> parameters = new Dictionary<string, string>();

        if (request == null)
            return parameters;

        string queryParams = Uri.UnescapeDataString(request.Url.Query);

        if (!string.IsNullOrEmpty(queryParams))
        {
            string[] pairs = queryParams.TrimStart('?').Split('&');
            foreach (string pair in pairs)
            {
                string[] keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                {
                    string key = keyValue[0];
                    string value = keyValue[1];
                    parameters[key] = value;
                }
            }
        }

        return parameters;
    }

    public static void SendResponce(HttpListenerResponse response, int status, string contentType, object obj)
    {
        response.StatusCode = status;

        response.ContentType = contentType;
        response.ContentEncoding = Encoding.UTF8;
        string responseString = contentType == "application/json" ? JSON.ToJSON(obj) : (string)obj;
        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        using Stream output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
    }

    public static void SendResponce(HttpListenerResponse response, HttpStatusCode status, string contentType,
        object obj) =>
        SendResponce(response, (int)status, contentType, obj);

    public static void SendResponce(HttpListenerResponse response, int status) =>
        SendResponce(response, status, "text/plain", "");

    public static void SendResponce(HttpListenerResponse response, HttpStatusCode status) =>
        SendResponce(response, (int)status, "text/plain", "");

    public static void SendResponce(HttpListenerResponse response, int status, string message) =>
        SendResponce(response, status, "text/plain", message);

    public static void SendResponce(HttpListenerResponse response, HttpStatusCode status, string message) =>
        SendResponce(response, status, "text/plain", message);
}

public class AuthData
{
    public string login;
    public string password;

    public AuthData() { }

    public AuthData(string login, string password)
    {
        this.login = login;
        this.password = password;
    }
}