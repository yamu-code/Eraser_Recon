namespace HorrorEngine
{
    public interface IResetable
    {
        void OnReset();

        int GetResetPriority() => 0;
    }
}