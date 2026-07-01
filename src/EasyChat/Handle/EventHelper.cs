using EasyChat.Models;
using EasyChat.Utilities;

namespace EasyChat.Handle
{
    public class EventHelper : Singleton<EventHelper>
    {
        public event Action? StartBlinkEvent;
        public event Action? StopBlinkEvent;
        public event Action? ClearNewMessage;
        public event Action<ChatMessage>? FileReceive;
        public event Action<string>? UnreadPreviewChanged;

        public void StartBlink()
        {
            StartBlinkEvent?.Invoke();
        }

        public void StopBlink()
        {
            StopBlinkEvent?.Invoke();
        }

        public void ClearNewMessageCount()
        {
            ClearNewMessage?.Invoke();
        }

        public void ReceiveFile(ChatMessage message)
        {
            FileReceive?.Invoke(message);
        }

        public void UpdateUnreadPreview(string preview)
        {
            UnreadPreviewChanged?.Invoke(preview);
        }
    }
}
