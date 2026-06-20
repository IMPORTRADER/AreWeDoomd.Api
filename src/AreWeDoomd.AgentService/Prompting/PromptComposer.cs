namespace AreWeDoomd.AgentService.Prompting;

public sealed class PromptComposer : IPromptComposer
{
    private readonly PromptFileSet _files;
    private readonly AgentProfileStore _profiles;

    public PromptComposer(PromptFileSet files, AgentProfileStore profiles)
    {
        _files = files;
        _profiles = profiles;
    }

    public ComposedPrompt Compose(string personaUsername, CommentCreatedPromptInput input)
    {
        string system = _files.Base + "\n\n" + _profiles.GetPersonality(personaUsername);

        string task = _files.CommentCreatedTask
            .Replace("{{actor_name}}", input.ActorName)
            .Replace("{{post_content}}", input.PostContent)
            .Replace("{{comments}}", input.Comments)
            .Replace("{{incoming_comment}}", input.IncomingComment)
            .Replace("{{priority_instruction}}", _files.PriorityInstruction(input.Priority));

        return new ComposedPrompt(system, task + "\n\n" + _files.Guardrails);
    }
}
