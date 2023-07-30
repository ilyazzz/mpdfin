namespace Mpdfin.Mpd;

partial class CommandHandler
{
    public Response Add(string uri)
    {
        AddId(uri);
        return new();
    }

    public Response AddId(string uri)
    {
        var guid = Guid.Parse(uri);
        var item = Db.Items.Find(item => item.Id == guid);

        if (item is not null)
        {
            var url = Db.GetAudioStreamUri(item.Id);
            var queueId = Player.Add(url, item);
            return new("Id"u8, queueId.ToString());
        }
        else
        {
            throw new FileNotFoundException($"Item {uri} not found");
        }
    }

    public Response PlaylistInfo()
    {
        Response response = new();

        int i = 0;
        foreach (var song in Player.Queue)
        {
            var itemResponse = song.GetResponse(i);
            response.Extend(itemResponse);
            i++;
        }

        return response;
    }

    public Response PlChanges(long version)
    {
        // Naive implementation
        if (version < Player.PlaylistVersion)
        {
            return PlaylistInfo();
        }
        else
        {
            return new();
        }
    }

    public Response Clear()
    {
        Player.ClearQueue();
        return new();
    }
}