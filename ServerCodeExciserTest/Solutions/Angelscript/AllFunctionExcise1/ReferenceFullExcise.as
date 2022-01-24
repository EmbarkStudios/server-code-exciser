class UReferenceFullExcise
{
#ifndef WITH_SERVER
	FVector FVectorReferenceDummy2;
#endif // WITH_SERVER

	FVector MyVector;

	const FVector& ReferenceFullExcise()
	{
#ifdef WITH_SERVER
		int SomethingBefore = 0;
		SomethingBefore++;

		// If it's a reference, we cannot return something on the stack, so it should be safe to just do the normal return.
		if(System::IsServer())
		{
			float ThisMustBeGuarded = 0.0f;
			ThisMustBeGuarded--;
			return MyVector;
		}
		else
		{
			int NotThisThough = 0;
			NotThisThough++;
		}

		int ButNotThis = 0;
		ButNotThis++;

		return MyVector;
#else
		return FVectorReferenceDummy2;
#endif // WITH_SERVER
	}
};
