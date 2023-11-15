using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace BuzzFreed
{
    public class DiscordSlashCommands : ModuleBase<SocketCommandContext>
    {
        // Here we can start adding the functionality for /quiz and /score
        // For now, they're placeholders
        public async Task QuizCommand()
        {
            await ReplyAsync("Starting a new quiz... (This is a placeholder)");
        }

        public async Task ScoreCommand()
        {
            await ReplyAsync("Fetching your score... (This is a placeholder)");
        }
    }
}
