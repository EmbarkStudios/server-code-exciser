class UReturnValueMixinTest
{
	mixin float Test()
	{
		ensure(System::IsServer());

		float ThisMustBeGuarded = 0.0f;
		ThisMustBeGuarded--;

		return ThisMustBeGuarded;
	}
};
