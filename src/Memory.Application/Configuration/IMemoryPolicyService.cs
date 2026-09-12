namespace Memory.Application.Configuration;

public interface IMemoryPolicyService
{
    MemoryPolicyResponse Get();
    MemoryPolicyResponse Set(MemoryPolicyKind policy);
}
