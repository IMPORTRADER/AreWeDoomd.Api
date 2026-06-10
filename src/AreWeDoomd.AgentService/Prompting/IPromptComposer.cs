namespace AreWeDoomd.AgentService.Prompting;

public interface IPromptComposer
{
    ComposedPrompt Compose(string personaUsername, CommentCreatedPromptInput input);
}
