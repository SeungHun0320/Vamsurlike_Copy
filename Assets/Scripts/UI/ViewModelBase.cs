using System;

namespace Vamsurlike.UI
{
    // ViewModel 공통 베이스 — UIEventHub 구독 수명주기 관리.
    // 상속 클래스는 Subscribe()/Unsubscribe()에서 이벤트를 등록/해제한다.
    // Bind()는 View의 OnEnable에서, Dispose()는 OnDisable에서 호출한다.
    public abstract class ViewModelBase : IDisposable
    {
        private bool disposed;
        private bool bound;

        protected abstract void Subscribe();
        protected abstract void Unsubscribe();

        // View.OnEnable에서 호출 — 중복 구독 방지를 위해 먼저 해제 후 재구독
        public void Bind()
        {
            if (disposed) return;
            if (bound) Unsubscribe();
            Subscribe();
            bound = true;
        }

        // View.OnDisable에서 호출 — 구독만 해제, disposed 플래그 없음 (재구독 가능)
        public void Unbind()
        {
            if (!bound) return;
            bound = false;
            Unsubscribe();
        }

        // 완전 파기 시 호출 (View가 Destroy될 때)
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            bound    = false;
            Unsubscribe();
        }
    }
}
