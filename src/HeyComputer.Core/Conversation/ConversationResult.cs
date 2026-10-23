namespace HeyComputer.Core.Conversation;

public sealed record ConversationResult(string Transcript, string ResponseText, TimeSpan Elapsed);
