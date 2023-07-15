using System.Globalization;
using System.Text;
using Serilog;

namespace Mpdfin;

public enum Command
{
    ping,

    status,
    currentsong,
    playid,
    pause,
    getvol,
    setvol,
    addid,
    playlistinfo,
    plchanges,
    find,
    tagtypes,

    command_list_begin,
    command_list_ok_begin,
    command_list_end,
}

public readonly record struct Request
{
    public readonly Command Command;
    public readonly List<string> Args;

    static Command ParseCommand(string rawCommand)
    {

        if (!int.TryParse(rawCommand, out int _) && Enum.TryParse(rawCommand, false, out Command command))
        {
            return command;
        }
        else
        {
            throw new Exception($"unknown command {rawCommand}");
        }
    }

    public Request(string raw)
    {
        var chars = raw.ToCharArray();

        StringBuilder rawCommandBuilder = new();
        int i;

        for (i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c == ' ')
            {
                var rawCommand = rawCommandBuilder.ToString();
                Command = ParseCommand(rawCommand);
                break;
            }
            else
            {
                rawCommandBuilder.Append(c);
            }
        }

        if (i == chars.Length)
        {
            Command = ParseCommand(rawCommandBuilder.ToString());
        }

        Args = new();
        StringBuilder currentArgBuilder = new();

        for (; i < chars.Length; i++)
        {
            var c = chars[i];
            switch (c)
            {
                case '"':
                    if (currentArgBuilder.Length > 0)
                    {
                        throw new Exception($"Unexpected data before quote: {currentArgBuilder}");
                    }

                    for (i++; i < chars.Length; i++)
                    {
                        var innerC = chars[i];

                        switch (innerC)
                        {
                            case '"':
                                var arg = currentArgBuilder.ToString();
                                Args.Add(arg);
                                currentArgBuilder.Clear();
                                break;
                            case '\\':
                                i++;

                                if (i < chars.Length)
                                {
                                    var escapedChar = chars[i];
                                    currentArgBuilder.Append(escapedChar);
                                }
                                else
                                {
                                    throw new Exception("No character after escape symbol");
                                }
                                break;
                            default:
                                currentArgBuilder.Append(innerC);
                                break;
                        }
                    }
                    break;
                case ' ':
                    if (currentArgBuilder.Length > 0)
                    {
                        var arg = currentArgBuilder.ToString();
                        Args.Add(arg);
                        currentArgBuilder.Clear();
                    }
                    break;
                case '\\':
                    i++;

                    if (i < chars.Length)
                    {
                        var escapedChar = chars[i];
                        currentArgBuilder.Append(escapedChar);
                    }
                    else
                    {
                        throw new Exception("No character after escape symbol");
                    }
                    break;
                default:
                    currentArgBuilder.Append(c);
                    break;
            }
        }

        if (currentArgBuilder.Length > 0)
        {
            var arg = currentArgBuilder.ToString();
            Args.Add(arg);
        }
    }
}
