namespace QuranSchool.Models.Response;

public class MessageResponse<T>
{
    public MessageResponse(T message)
    {
        Message = message;
    }

    public T Message { get; }
}