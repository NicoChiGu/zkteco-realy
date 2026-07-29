using ZktecoRelay.Models;

namespace ZktecoRelay.Devices;

internal sealed partial class ZktecoComClient
{
    public FingerprintTemplateResult GetFingerprint(string enrollNumber, int fingerIndex)
    {
        ThrowIfDisposed();
        string data = string.Empty;
        var length = 0;
        if (!_sdk.SSR_GetUserTmpStr(MachineNumber, enrollNumber, fingerIndex, out data, out length))
        {
            throw VendorFailure("SSR_GetUserTmpStr");
        }

        return new FingerprintTemplateResult(enrollNumber, fingerIndex, data, length);
    }

    public OperationResult SetFingerprint(string enrollNumber, FingerprintTemplateRequest request)
    {
        ThrowIfDisposed();
        ValidateWritableEnrollNumber(enrollNumber);
        var ok = _sdk.SSR_SetUserTmpStr(MachineNumber, enrollNumber, request.FingerIndex, request.TemplateData);
        if (!ok)
        {
            return Failure("SSR_SetUserTmpStr");
        }

        _sdk.RefreshData(MachineNumber);
        return new OperationResult(true);
    }

    public OperationResult DeleteFingerprint(string enrollNumber, int fingerIndex)
    {
        ThrowIfDisposed();
        ValidateWritableEnrollNumber(enrollNumber);
        var ok = _sdk.SSR_DeleteEnrollDataExt(MachineNumber, enrollNumber, fingerIndex);
        if (!ok)
        {
            return Failure("SSR_DeleteEnrollDataExt");
        }

        _sdk.RefreshData(MachineNumber);
        return new OperationResult(true);
    }

    public FaceTemplateResult GetFace(string enrollNumber, int faceIndex)
    {
        ThrowIfDisposed();
        string data = string.Empty;
        var length = 0;
        if (!_sdk.GetUserFaceStr(MachineNumber, enrollNumber, faceIndex, out data, out length))
        {
            throw VendorFailure("GetUserFaceStr");
        }

        return new FaceTemplateResult(enrollNumber, faceIndex, data, length);
    }

    public OperationResult SetFace(string enrollNumber, FaceTemplateRequest request)
    {
        ThrowIfDisposed();
        ValidateWritableEnrollNumber(enrollNumber);
        var ok = _sdk.SetUserFaceStr(
            MachineNumber,
            enrollNumber,
            request.FaceIndex,
            request.TemplateData,
            request.TemplateData.Length);

        if (!ok)
        {
            return Failure("SetUserFaceStr");
        }

        _sdk.RefreshData(MachineNumber);
        return new OperationResult(true);
    }

    public OperationResult DeleteFace(string enrollNumber, int faceIndex)
    {
        ThrowIfDisposed();
        ValidateWritableEnrollNumber(enrollNumber);
        var ok = _sdk.DelUserFace(MachineNumber, enrollNumber, faceIndex);
        if (!ok)
        {
            return Failure("DelUserFace");
        }

        _sdk.RefreshData(MachineNumber);
        return new OperationResult(true);
    }

    public OperationResult UploadUserPhoto(string enrollNumber, UserPhotoRequest request)
    {
        ThrowIfDisposed();
        EnsureUserPhotoSupported();
        var bytes = Convert.FromBase64String(request.Base64Jpeg);
        ValidatePhoto(enrollNumber, bytes);
        var tempDirectory = CreatePhotoTempDirectory();
        Directory.CreateDirectory(tempDirectory);
        var fileName = request.VisibleLightFacePhoto
            ? $"verify_biophoto_9_{enrollNumber}.jpg"
            : $"{enrollNumber}.jpg";
        var fullPath = Path.Combine(tempDirectory, fileName);

        try
        {
            File.WriteAllBytes(fullPath, bytes);
            var ok = request.VisibleLightFacePhoto
                ? _sdk.SendUserFacePhoto(MachineNumber, fullPath)
                : _sdk.UploadUserPhoto(MachineNumber, fullPath);

            return ok ? new OperationResult(true) : Failure(request.VisibleLightFacePhoto ? "SendUserFacePhoto" : "UploadUserPhoto");
        }
        finally
        {
            TryDeletePhotoDirectory(tempDirectory);
        }
    }

    public UserPhotoResult DownloadUserPhoto(string enrollNumber)
    {
        ThrowIfDisposed();
        EnsureUserPhotoSupported();
        ValidatePhotoEnrollNumber(enrollNumber);
        var tempDirectory = CreatePhotoTempDirectory();
        Directory.CreateDirectory(tempDirectory);
        var fileName = $"{enrollNumber}.jpg";
        var fullPath = Path.Combine(tempDirectory, fileName);

        try
        {
            if (!_sdk.DownloadUserPhoto(
                    MachineNumber,
                    fileName,
                    tempDirectory + Path.DirectorySeparatorChar))
            {
                throw VendorFailure("DownloadUserPhoto");
            }

            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException(
                    "DownloadUserPhoto succeeded but the SDK did not create the expected JPG file.");
            }

            var bytes = File.ReadAllBytes(fullPath);
            ValidatePhoto(enrollNumber, bytes);
            return new UserPhotoResult(
                enrollNumber,
                fileName,
                Convert.ToBase64String(bytes),
                bytes.Length);
        }
        finally
        {
            TryDeletePhotoDirectory(tempDirectory);
        }
    }

    private static string CreatePhotoTempDirectory() =>
        Path.Combine(
            Path.GetTempPath(),
            "zkteco-relay-photos",
            Guid.NewGuid().ToString("N"));

    private static void ValidatePhoto(string enrollNumber, byte[] bytes)
    {
        ValidatePhotoEnrollNumber(enrollNumber);
        if (bytes.Length is < 4 or > 10 * 1024 * 1024)
        {
            throw new ArgumentException("User photo must be a JPG file between 4 bytes and 10 MB.");
        }

        if (bytes[0] != 0xFF || bytes[1] != 0xD8 || bytes[2] != 0xFF)
        {
            throw new ArgumentException("User photo must contain valid JPG data.");
        }
    }

    private static void ValidatePhotoEnrollNumber(string enrollNumber)
    {
        ValidateWritableEnrollNumber(enrollNumber);
    }

    private void EnsureUserPhotoSupported()
    {
        try
        {
            if (!_sdk.IsNewFirmwareMachine(MachineNumber))
            {
                throw new CapabilityNotSupportedException(
                    "The connected device firmware does not support user photo transfer.");
            }
        }
        catch (CapabilityNotSupportedException)
        {
            throw;
        }
        catch
        {
            // Some SDK registrations do not expose the probe method. In that
            // case the capability is unknown and the actual photo operation is
            // allowed to decide whether it is supported.
        }
    }

    private static void TryDeletePhotoDirectory(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { }
    }
}
