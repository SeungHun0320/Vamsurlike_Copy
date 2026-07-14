using System;
using System.Runtime.CompilerServices;
using UnityEngine.Networking;

namespace Vamsurlike.Network
{
    // UnityWebRequestAsyncOperation을 async/await로 기다릴 수 있게 해주는 확장 — 프로젝트에 기존 HTTP
    // 유틸리티가 없어 신규 추가. UgsJwtVerifier의 JWKS 조회에서 사용한다.
    internal static class UnityWebRequestAwaiterExtensions
    {
        public static UnityWebRequestAwaiter GetAwaiter(this UnityWebRequestAsyncOperation asyncOp)
        {
            return new UnityWebRequestAwaiter(asyncOp);
        }
    }

    internal readonly struct UnityWebRequestAwaiter : INotifyCompletion
    {
        private readonly UnityWebRequestAsyncOperation asyncOp;

        public UnityWebRequestAwaiter(UnityWebRequestAsyncOperation asyncOp)
        {
            this.asyncOp = asyncOp;
        }

        public bool IsCompleted => asyncOp.isDone;

        public void GetResult()
        {
        }

        public void OnCompleted(Action continuation)
        {
            asyncOp.completed += _ => continuation();
        }
    }
}
