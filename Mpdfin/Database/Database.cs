using System.Diagnostics.CodeAnalysis;
using Jellyfin.Sdk;
using Serilog;

namespace Mpdfin.DB;

public class Database
{
    readonly ItemsClient itemsClient;
    readonly UserViewsClient userViewsClient;
    readonly PlaystateClient playstateClient;

    readonly DatabaseStorage Storage;
    readonly UserDto CurrentUser;
    readonly SdkClientSettings Settings;

    public event EventHandler? OnDatabaseUpdated;
    public event EventHandler? OnUpdate;

    public List<BaseItemDto> Items
    {
        get => Storage.Items;
        private set => Storage.Items = value;
    }

    public BaseItemDto? GetItem(Guid id)
    {
        return Items.Find(item => item.Id == id);
    }

    public Database(string serverUrl, DatabaseStorage storage)
    {
        Storage = storage;

        HttpClient httpClient = new();

        var settings = ClientSettings();
        settings.BaseUrl = serverUrl;
        settings.AccessToken = storage.AuthenticationResult.AccessToken;
        Settings = settings;

        CurrentUser = storage.AuthenticationResult.User;

        itemsClient = new(settings, httpClient);
        userViewsClient = new(settings, httpClient);
        playstateClient = new(settings, httpClient);
    }

    public static Task<AuthenticationResult> Authenticate(string serverUrl, string username, string password)
    {
        var settings = ClientSettings();
        settings.BaseUrl = serverUrl;

        HttpClient httpClient = new();
        UserClient userClient = new(settings, httpClient);

        AuthenticateUserByName request = new()
        {
            Username = username,
            Pw = password
        };

        return userClient.AuthenticateUserByNameAsync(request);
    }

    static SdkClientSettings ClientSettings()
    {
        SdkClientSettings settings = new();
        settings.InitializeClientSettings("dotnet test", "0.0.1", Environment.MachineName, "1");
        return settings;
    }

    [RequiresUnreferencedCode("Serialization")]
    public async Task Update()
    {
        Log.Information("Updating database");
        if (OnUpdate is not null)
        {
            OnUpdate(this, new());
        }

        var views = await userViewsClient.GetUserViewsAsync(CurrentUser.Id);

        var musicCollection = views.Items.Single(item => item.CollectionType == "music");

        if (musicCollection is not null)
        {
            Log.Debug($"Using music collection with id {musicCollection.Id}");

            var itemsResponse = await itemsClient.GetItemsByUserIdAsync(
                CurrentUser.Id,
                recursive: true,
                parentId: musicCollection.Id,
                includeItemTypes: new[] { BaseItemKind.Audio });

            Items = itemsResponse.Items.ToList();

            Log.Debug($"Loaded {Items.Count} items");

            await Storage.Save();

            if (OnUpdate is not null)
            {
                OnUpdate(this, new());
            }
            if (OnDatabaseUpdated is not null)
            {
                OnDatabaseUpdated(this, new());
            }
        }
        else
        {
            if (OnDatabaseUpdated is not null)
            {
                OnDatabaseUpdated(this, new());
            }
            throw new Exception("Server has no music library configured");
        }
    }

    public Uri GetAudioStreamUri(Guid itemId)
    {
        return new Uri($"{Settings.BaseUrl}/Audio/{itemId}/universal?api_key={Settings.AccessToken}&UserId={CurrentUser?.Id}&Container=opus,webm|opus,mp3,aac,m4a|aac,m4b|aac,flac,webma,webm|webma,wav,ogg");
    }
}
