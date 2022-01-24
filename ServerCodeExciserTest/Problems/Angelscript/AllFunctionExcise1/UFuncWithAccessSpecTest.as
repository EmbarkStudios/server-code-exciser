class UFuncWithAccessSpecTest
{
	UFUNCTION(Category = "Mwap")
	protected float Test()
	{
		float ThisMustBeGuarded = 0.0f;
		ThisMustBeGuarded--;

		return ThisMustBeGuarded;
	}
};
