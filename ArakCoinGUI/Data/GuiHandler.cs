namespace ArakCoin_GUI.Data;

public static class GuiHandler
{
    public static event EventHandler<string>? stateUpdate; //refresh components to check for a state update
    
    public static void OnStateUpdate(string update)
    {
        EventHandler<string>? handler = stateUpdate;
        if (handler is not null)
            handler(null, update);
    }
}