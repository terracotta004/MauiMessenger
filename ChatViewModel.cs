using System.Collections.ObjectModel;
using System.Windows.Input;

public class ChatViewModel
{
    public ObservableCollection<Message> Messages { get; set; } = new();
    public ICommand SendMessageCommand { get; }

    public ChatViewModel()
    {
        SendMessageCommand = new Command(SendMessage);
    }

    private void SendMessage()
    {
        Messages.Add(new Message { Text = "Hello, world!" });
    }
}

public class Message
{
    public string Text { get; set; }
}
