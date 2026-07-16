using ZktecoRelay.Models;

namespace ZktecoRelay.Devices;

internal sealed partial class ZktecoComClient
{
    public IReadOnlyList<UserInfo> GetUsers()
    {
        ThrowIfDisposed();
        if (!_sdk.ReadAllUserID(MachineNumber))
        {
            throw VendorFailure("ReadAllUserID");
        }

        var users = new List<UserInfo>();
        while (true)
        {
            string enrollNumber = string.Empty;
            string name = string.Empty;
            string password = string.Empty;
            var privilege = 0;
            var enabled = false;

            if (!_sdk.SSR_GetAllUserInfo(MachineNumber, out enrollNumber, out name, out password, out privilege, out enabled))
            {
                break;
            }

            string cardNumber = string.Empty;
            try
            {
                _sdk.GetStrCardNumber(out cardNumber);
            }
            catch
            {
                cardNumber = string.Empty;
            }

            users.Add(new UserInfo(enrollNumber, name, privilege, enabled, cardNumber, !string.IsNullOrEmpty(password)));
        }

        return users;
    }

    public UserInfo GetUser(string enrollNumber)
    {
        ThrowIfDisposed();
        string name = string.Empty;
        string password = string.Empty;
        var privilege = 0;
        var enabled = false;

        if (!_sdk.SSR_GetUserInfo(MachineNumber, enrollNumber, out name, out password, out privilege, out enabled))
        {
            throw VendorFailure("SSR_GetUserInfo");
        }

        string cardNumber = string.Empty;
        try
        {
            _sdk.GetStrCardNumber(out cardNumber);
        }
        catch
        {
            cardNumber = string.Empty;
        }

        return new UserInfo(enrollNumber, name, privilege, enabled, cardNumber, !string.IsNullOrEmpty(password));
    }

    public OperationResult UpsertUser(string enrollNumber, UpsertUserRequest request)
    {
        ThrowIfDisposed();
        var password = request.Password;
        var cardNumber = request.CardNumber;

        if (password is null || cardNumber is null)
        {
            string existingName = string.Empty;
            string existingPassword = string.Empty;
            var existingPrivilege = 0;
            var existingEnabled = false;
            if (_sdk.SSR_GetUserInfo(MachineNumber, enrollNumber, out existingName, out existingPassword, out existingPrivilege, out existingEnabled))
            {
                password ??= existingPassword;
                if (cardNumber is null)
                {
                    string existingCard = string.Empty;
                    try { _sdk.GetStrCardNumber(out existingCard); } catch { }
                    cardNumber = existingCard;
                }
            }
        }

        _sdk.SetStrCardNumber(cardNumber ?? string.Empty);
        var ok = _sdk.SSR_SetUserInfo(
            MachineNumber,
            enrollNumber,
            request.Name,
            password ?? string.Empty,
            request.Privilege,
            request.Enabled);

        if (!ok)
        {
            return Failure("SSR_SetUserInfo");
        }

        _sdk.RefreshData(MachineNumber);
        return new OperationResult(true);
    }

    public OperationResult DeleteUser(string enrollNumber)
    {
        ThrowIfDisposed();
        var ok = _sdk.SSR_DeleteEnrollDataExt(MachineNumber, enrollNumber, 12);
        if (!ok)
        {
            return Failure("SSR_DeleteEnrollDataExt");
        }

        _sdk.RefreshData(MachineNumber);
        return new OperationResult(true);
    }

    private InvalidOperationException VendorFailure(string operation) =>
        new($"{operation} failed. Vendor error: {GetLastError()}.");

    private OperationResult Failure(string operation)
    {
        var code = GetLastError();
        return new OperationResult(false, code, $"{operation} failed.");
    }
}
