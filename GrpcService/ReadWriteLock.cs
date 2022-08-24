namespace GrpcService
{
    public class ReadWriteLock : IDisposable
    {
        //Данный класс является оберткой для улучшения читаемости кода.
        //Вместо конструкции try-finally используется конструкция, похожая на lock
        public struct WriteLockToken : IDisposable
        {
            private readonly ReaderWriterLockSlim _locker;
            public WriteLockToken(ReaderWriterLockSlim locker)
            {
                this._locker = locker;
                locker.EnterWriteLock();
            }
            public void Dispose() => _locker.ExitWriteLock();
        }

        public struct ReadLockToken : IDisposable
        {
            private readonly ReaderWriterLockSlim _locker;
            public ReadLockToken(ReaderWriterLockSlim locker)
            {
                this._locker = locker;
                locker.EnterReadLock();
            }
            public void Dispose() => _locker.ExitReadLock();
        }

        private readonly ReaderWriterLockSlim _locker = new ReaderWriterLockSlim();

        public ReadLockToken ReadLock() => new ReadLockToken(_locker);
        public WriteLockToken WriteLock() => new WriteLockToken(_locker);

        public void Dispose() => _locker.Dispose();
    }
}
