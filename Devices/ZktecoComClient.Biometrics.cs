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
        var bytes = Convert.FromBase64String(request.Base64Jpeg);
        var tempDirectory = Path.Combine(Path.GetTempPath(), "zkteco-relay-photos");
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
            try { File.Delete(fullPath); } catch { }
        }
    }
}
