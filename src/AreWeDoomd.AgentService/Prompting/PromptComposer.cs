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
            .Replace("{{priority_instruction}}", _files.PriorityInstruction(input.Priority));

        return new ComposedPrompt(system, task + "\n\n" + _files.Guardrails);
    }
}
