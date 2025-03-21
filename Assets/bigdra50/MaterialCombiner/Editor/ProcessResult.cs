#if UNITY_EDITOR
namespace MaterialCombiner.Editor
{
    public readonly struct ProcessResult
    {
        public readonly bool IsSuccess;
        public readonly string Message;

        private ProcessResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message;
        }

        public static ProcessResult Success() => Success("success");
        public static ProcessResult Success(string message) => new(true, message);
        public static ProcessResult Failure(string message) => new(false, message);
    }
}
#endif
