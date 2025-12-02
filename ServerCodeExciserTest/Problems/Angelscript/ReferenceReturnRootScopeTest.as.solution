FVector& TestReferenceReturnRootScope()
{
	int SomethingBefore = 0;
	SomethingBefore++;

	// If it's a reference, we cannot return something on the stack.
	// This is also not in a class, so we can't add a member. Just leave this alone (for now).
	if(System::IsServer())
	{
		float ThisMustBeGuarded = 0.0f;
		ThisMustBeGuarded--;
		return OtherSystem();
	}
	else
	{
		int NotThisThough = 0;
		NotThisThough++;
	}

	int ButNotThis = 0;
	ButNotThis++;

	return OtherSystem();
}