using Serilog;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    readonly Player.Player Player;
    readonly Database Db;
    public readonly ClientNotificationsReceiver NotificationsReceiver;

    public CommandHandler(Player.Player player, Database db)
    {
        Player = player;
        Db = db;
        NotificationsReceiver = new();

        Player.OnSubsystemUpdate += (e, args) => NotificationsReceiver.SendEvent(args.Subsystem);
        Db.OnUpdate += (e, args) => NotificationsReceiver.SendEvent(Subsystem.update);
        Db.OnDatabaseUpdated += (e, args) => NotificationsReceiver.SendEvent(Subsystem.database);
    }

    public async Task HandleStream(ClientStream stream)
    {
        Request? request;
        while ((request = await stream.ReadRequest()) is not null)
        {
            try
            {
                var response = await HandleRequest(request.Value, stream);

                if (!stream.EndOfStream && response is not null)
                {
                    await stream.WriteResponse(response.Value);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Error occured when processing request `{request}`: {ex}");
                await stream.WriteError(Ack.UNKNOWN, messageText: ex.Message);
            }
        }
    }

    async Task<Response?> HandleRequest(Request request, ClientStream stream)
    {
        return request.Command switch
        {
            Command.command_list_begin => await HandleCommandList(stream, false),
            Command.command_list_ok_begin => await HandleCommandList(stream, true),
            Command.idle => await Idle(stream, request.Args),
            Command.noidle => null,
            Command.ping => new(),
            Command.status => Status(),
            Command.currentsong => CurrentSong(),
            Command.play => Play(int.Parse(request.Args[0])),
            Command.playid => PlayId(int.Parse(request.Args[0])),
            Command.pause => Pause(request.Args.ElementAtOrDefault(0)),
            Command.stop => Stop(),
            Command.seek => Seek(int.Parse(request.Args[0]), double.Parse(request.Args[1])),
            Command.seekid => SeekId(int.Parse(request.Args[0]), double.Parse(request.Args[1])),
            Command.seekcur => SeekCur(double.Parse(request.Args[0])),
            Command.next => Next(),
            Command.previous => Previous(),
            Command.getvol => GetVol(),
            Command.setvol => SetVol(int.Parse(request.Args[0])),
            Command.volume => Volume(int.Parse(request.Args[0])),
            Command.add => Add(request.Args[0]),
            Command.addid => AddId(request.Args[0]),
            Command.playlistinfo => PlaylistInfo(),
            Command.plchanges => PlChanges(long.Parse(request.Args[0])),
            Command.tagtypes => TagTypes(),
            Command.list => List(Enum.Parse<Tag>(request.Args[0], true)),
            Command.lsinfo => LsInfo(request.Args.FirstOrDefault()),
            Command.find => Find(Filter.ParseFilters(request.Args)),
            Command.outputs => Outputs(),
            Command.stats => Stats(),
            Command.commands => Commands(),
            Command.decoders => Decoders(),
            _ => throw new NotImplementedException($"Command {request.Command} not implemented or cannot be called in the current context"),
        };
    }

    async Task<Response> HandleCommandList(ClientStream stream, bool printOk)
    {
        List<Request> requestList = new();

        bool end = false;
        Log.Debug("Processing command list");
        Request? request;
        while ((request = await stream.ReadRequest()) is not null)
        {
            switch (request.Value.Command)
            {
                case Command.command_list_end:
                    Log.Debug("Exiting command list");
                    end = true;
                    break;
                default:
                    Log.Debug($"Queueing command {request.Value.Command}");
                    requestList.Add(request.Value);
                    break;
            }

            if (end)
            {
                break;
            }
        }

        Response totalResponse = new();

        foreach (var queuedRequest in requestList)
        {
            var response = await HandleRequest(queuedRequest, stream);
            totalResponse.Extend(response.Value);

            if (printOk)
            {
                totalResponse.AddListOk();
            }
        }

        return totalResponse;
    }
}
