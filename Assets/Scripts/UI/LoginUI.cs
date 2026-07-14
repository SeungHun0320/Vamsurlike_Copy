using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.Network;

namespace Vamsurlike.UI
{
    // MainMenu 씬의 로그인/회원가입 패널. 성공 시 LoggedIn을 발행 — MainMenuUI가 이를 받아 connectPanel을 연다.
    public class LoginUI : MonoBehaviour
    {
        private const string PrefKeyUsername = "LoginUsername";

        // 로그인한 아이디를 그대로 게임 내 닉네임으로 쓴다 — 별도 닉네임 입력칸 없음.
        public event Action<string> LoggedIn;

        [SerializeField] private TMP_InputField  usernameInput;
        [SerializeField] private TMP_InputField  passwordInput;
        [SerializeField] private Button          loginButton;
        [SerializeField] private Button          signUpButton;
        [SerializeField] private TextMeshProUGUI statusText;

        private bool isBusy;

        private void Awake()
        {
            if (passwordInput != null)
                passwordInput.contentType = TMP_InputField.ContentType.Password;

            if (usernameInput != null)
                usernameInput.text = PlayerPrefs.GetString(PrefKeyUsername, "");

            if (loginButton  != null) loginButton.onClick.AddListener(() => _ = SubmitAsync(isSignUp: false));
            if (signUpButton != null) signUpButton.onClick.AddListener(() => _ = SubmitAsync(isSignUp: true));
        }

        private void OnEnable()
        {
            // 이미 로그인된 상태로 이 패널이 다시 활성화된 경우(로비 복귀 등) 즉시 통과시킨다.
            if (NetworkBootstrapper.IsSignedIn)
                LoggedIn?.Invoke(PlayerPrefs.GetString(PrefKeyUsername, ""));
        }

        private async Task SubmitAsync(bool isSignUp)
        {
            if (isBusy) return;

            string username = usernameInput != null ? usernameInput.text.Trim() : "";
            string password = passwordInput != null ? passwordInput.text : "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                SetStatus("아이디와 비밀번호를 입력하세요.");
                return;
            }

            if (!NetworkBootstrapper.IsUgsReady)
            {
                SetStatus("서버 연결을 초기화하는 중입니다. 잠시 후 다시 시도하세요.");
                return;
            }

            isBusy = true;
            SetInteractable(false);
            SetStatus(isSignUp ? "회원가입 중..." : "로그인 중...");

            try
            {
                if (isSignUp)
                    await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
                else
                    await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);

                PlayerPrefs.SetString(PrefKeyUsername, username);
                SetStatus(isSignUp ? "회원가입 완료." : "로그인 완료.");
                LoggedIn?.Invoke(username);
            }
            catch (AuthenticationException e)
            {
                SetStatus(DescribeAuthError(e));
            }
            catch (RequestFailedException e)
            {
                SetStatus($"요청 실패 ({e.ErrorCode}): {e.Message}");
            }
            finally
            {
                isBusy = false;
                SetInteractable(true);
            }
        }

        private void SetInteractable(bool interactable)
        {
            if (usernameInput != null) usernameInput.interactable = interactable;
            if (passwordInput != null) passwordInput.interactable = interactable;
            if (loginButton   != null) loginButton.interactable   = interactable;
            if (signUpButton  != null) signUpButton.interactable  = interactable;
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            Debug.Log($"[{nameof(LoginUI)}] {message}");
        }

        private static string DescribeAuthError(AuthenticationException e)
        {
            if (e.ErrorCode == AuthenticationErrorCodes.InvalidParameters)
                return "아이디/비밀번호 형식이 올바르지 않습니다. (아이디 3~20자, 비밀번호 8자 이상 대소문자·숫자·특수문자 포함)";
            if (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                return "이미 사용 중인 아이디입니다.";
            return $"로그인 실패 ({e.ErrorCode}): {e.Message}";
        }
    }
}
