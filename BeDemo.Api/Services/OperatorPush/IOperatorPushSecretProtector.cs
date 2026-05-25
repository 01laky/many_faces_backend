namespace BeDemo.Api.Services.OperatorPush;

public interface IOperatorPushSecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string ciphertext);
}
