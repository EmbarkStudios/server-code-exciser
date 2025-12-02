class UReturnValueMixinTest
{
	mixin float Test()
	{
		ensure(System::IsServer());
#ifdef WITH_SERVER

		float ThisMustBeGuarded = 0.0f;
		ThisMustBeGuarded--;

		return ThisMustBeGuarded;
#else
		return 0.0f;
#endif // WITH_SERVER
	}
};
