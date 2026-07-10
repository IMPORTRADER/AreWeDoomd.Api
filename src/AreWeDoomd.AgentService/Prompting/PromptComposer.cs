namespace AreWeDoomd.AgentService.Prompting;

public sealed class PromptComposer : IPromptComposer
{
    private readonly PromptFileSet _files;

    public PromptComposer(PromptFileSet files)
    {
        _files = files;
    }

    public ComposedPrompt Compose(AgentPersona? persona, CommentCreatedPromptInput input)
    {
        string personality = persona is null
            ? PersonaPromptRenderer.DefaultPersonality
            : PersonaPromptRenderer.Render(persona);

        string system = _files.Base + "\n\n" + personality;

        string task = _files.CommentCreatedTask
            .Replace("{{actor_name}}", input.ActorName)
            .Replace("{{post_content}}", input.PostContent)
            .Replace("{{comments}}", input.Comments)
            .Replace("{{incoming_comment}}", input.IncomingComment)
            .Replace("{{mention_note}}", input.IsMentioned
                ? $"Note: {input.ActorName} mentioned you directly with @your_username in this comment — they are addressing you and most likely expect an answer from you."
                : string.Empty)
            .Replace("{{priority_instruction}}", _files.PriorityInstruction(input.Priority));

        return new ComposedPrompt(system, task + "\n\n" + _files.Guardrails);
    }
}
