using System.Net;

namespace ZktecoRelay.Hosting;

internal sealed class IpAccessPolicy
{
    private readonly IReadOnlyList<IpNetwork> _networks;

    private IpAccessPolicy(IReadOnlyList<IpNetwork> networks)
    {
        _networks = networks;
    }

    public static IpAccessPolicy Parse(string? value)
    {
        var rawEntries = string.IsNullOrWhiteSpace(value)
            ? new[] { "127.0.0.1/32", "::1/128" }
            : value.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var networks = new List<IpNetwork>();
        foreach (var entry in rawEntries)
        {
            networks.Add(IpNetwork.Parse(entry));
        }

        if (networks.Count == 0)
        {
            throw new InvalidOperationException("At least one allowed IP address or CIDR network is required.");
        }

        return new IpAccessPolicy(networks);
    }

    public bool IsAllowed(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return _networks.Any(network => network.Contains(normalized));
    }

    private sealed record IpNetwork(IPAddress Address, int PrefixLength)
    {
        public static IpNetwork Parse(string value)
        {
            var parts = value.Trim().Split('/', 2, StringSplitOptions.TrimEntries);
            if (!IPAddress.TryParse(parts[0], out var address))
            {
                throw new InvalidOperationException($"Invalid allowed IP address or CIDR network: {value}");
            }

            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            var maximumPrefix = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
            var prefixLength = parts.Length == 1
                ? maximumPrefix
                : int.TryParse(parts[1], out var parsedPrefix)
                    ? parsedPrefix
                    : -1;

            if (prefixLength < 0 || prefixLength > maximumPrefix)
            {
                throw new InvalidOperationException($"Invalid CIDR prefix length: {value}");
            }

            return new IpNetwork(Mask(address, prefixLength), prefixLength);
        }

        public bool Contains(IPAddress candidate)
        {
            if (candidate.AddressFamily != Address.AddressFamily)
            {
                return false;
            }

            return Mask(candidate, PrefixLength).Equals(Address);
        }

        private static IPAddress Mask(IPAddress address, int prefixLength)
        {
            var bytes = address.GetAddressBytes();
            var fullBytes = prefixLength / 8;
            var remainingBits = prefixLength % 8;

            if (remainingBits > 0 && fullBytes < bytes.Length)
            {
                bytes[fullBytes] &= (byte)(0xFF << (8 - remainingBits));
                fullBytes++;
            }

            for (var index = fullBytes; index < bytes.Length; index++)
            {
                bytes[index] = 0;
            }

            return new IPAddress(bytes);
        }
    }
}
