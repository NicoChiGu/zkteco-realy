namespace ZktecoRelay.Devices;

public sealed class DeviceUnavailableException : Exception
{
    public DeviceUnavailableException(string message, int? vendorErrorCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        VendorErrorCode = vendorErrorCode;
    }

    public int? VendorErrorCode { get; }
}

public sealed class CapabilityNotSupportedException : Exception
{
    public CapabilityNotSupportedException(string message)
        : base(message)
    {
    }
}

public sealed class DeviceOperationException : Exception
{
    public DeviceOperationException(string message, int? vendorErrorCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        VendorErrorCode = vendorErrorCode;
    }

    public int? VendorErrorCode { get; }
}

public sealed class VisibleLightFacePhotoNotFoundException : Exception
{
    public VisibleLightFacePhotoNotFoundException(string message)
        : base(message)
    {
    }
}
