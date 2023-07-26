using System.Diagnostics;
using LibVLCSharp.Shared;
using Microsoft.VisualBasic;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    Response Status()
    {
        Response response = new();

        response.Add("repeat"u8, "0"u8);
        response.Add("random"u8, "0"u8);
        response.Add("single"u8, "0"u8);
        response.Add("consume"u8, "0"u8);

        if (Player.CurrentPos is not null)
        {
            response.Add("song"u8, Player.CurrentPos.Value.ToU8String());
            response.Add("songid"u8, Player.CurrentSong!.Value.Id.ToU8String());
        }

        response.Add("volume"u8, Player.Volume.ToU8String());
        response.Add("state"u8, Player.State switch
        {
            VLCState.Playing => "play"u8,
            VLCState.Paused => "pause"u8,
            _ => "stop"u8,
        });
        response.Add("playlist"u8, Player.PlaylistVersion.ToU8String());
        response.Add("playlistlength"u8, Player.Queue.Count.ToU8String());
        response.Add("elapsed"u8, Player.Elapsed.ToU8String());
        response.Add("time"u8, $"{(int)Player.Elapsed}:{Player.Duration}");
        response.Add("duration"u8, Player.Duration.ToU8String());

        return response;
    }

    Response CurrentSong()
    {
        var currentSong = Player.CurrentSong;
        if (currentSong is not null)
        {
            return currentSong.Value.GetResponse(Player.CurrentPos);
        }
        else
        {
            return new();
        }
    }

    Response Stats()
    {
        Response response = new();

        response.Add("artists"u8, Db.Items.SelectMany(item => item.GetTagValue(Tag.Artist)!).Distinct().Count().ToU8String());
        response.Add("albums"u8, Db.Items.SelectMany(item => item.GetTagValue(Tag.Album)!).Distinct().Count().ToU8String());
        response.Add("songs"u8, Db.Items.Count.ToU8String());

        return response;
    }

    async Task<Response> Idle(ClientStream stream, List<string> args)
    {
        var subsystems = args.Count > 0
            ? args.Select(arg => Enum.Parse<Subsystem>(arg, true)).ToArray()
            : null;

        using CancellationTokenSource source = new();

        var notificationTask = NotificationsReceiver.WaitForEvent(subsystems, source.Token);
        var incomingCommandTask = stream.ReadRequest(source.Token);

        var finishedTask = await Task.WhenAny(notificationTask, incomingCommandTask);
        source.Cancel();

        if (finishedTask == notificationTask)
        {
            Response response = new();

            foreach (var subsystem in notificationTask.Result)
            {
                response.Add("changed"u8, Enum.GetName(subsystem));
            }

            return response;
        }
        else if (finishedTask == incomingCommandTask)
        {
            if (incomingCommandTask.Result?.Command == Command.noidle || incomingCommandTask.Result is null)
            {
                return new();
            }
            else
            {
                throw new Exception($"Only `noidle` can be called when idling, got `{incomingCommandTask.Result?.Command}`");
            }
        }
        else
        {
            throw new UnreachableException();
        }
    }
}
