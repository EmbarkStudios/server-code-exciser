class UReferenceReturnTestTest
{
	UReferenceReturnTestTest::FChildType MyType;

	struct FChildType
	{
		FVector Lel;
	};

	const UReferenceReturnTestTest::FChildType& Test()
	{
		int SomethingBefore = 0;
		SomethingBefore++;

		// If it's a reference, we cannot return something on the stack, so it should be safe to just do the normal return.
		if(System::IsServer())
		{
			float ThisMustBeGuarded = 0.0f;
			ThisMustBeGuarded--;
			return MyType;
		}
		else
		{
			int NotThisThough = 0;
			NotThisThough++;
		}

		int ButNotThis = 0;
		ButNotThis++;

		return MyType;
	}
};
