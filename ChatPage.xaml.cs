namespace MauiMessenger
{
    public partial class ChatPage : ContentPage
    {
        public ChatPage()
        {
            InitializeComponent();
            BindingContext = new ChatViewModel();
        }
    }
}
