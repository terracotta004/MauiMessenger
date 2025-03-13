using System.Collections.ObjectModel;
using System.Windows.Input;

public class ChatViewModel
{
    public ObservableCollection<Message> Messages { get; set; } = new();
    public ICommand SendMessageCommand { get; }

    public ChatViewModel(Entry entry)
    {
        SendMessageCommand = new Command(() => SendMessage(entry.Text));
    }

    private void SendMessage(string text)
    {
        Messages.Add(new Message { Text = text });

        return;
    }
}

public class Message
{
    public string Text { get; set; }
}
