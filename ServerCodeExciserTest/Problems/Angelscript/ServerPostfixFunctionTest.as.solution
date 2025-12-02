class UServerPostfixFunctionTest
{
	void Test_Server()
	{
#ifdef WITH_SERVER
		int SomethingBefore = 0;
		SomethingBefore++;

		if(System::IsServer())
		{
			float AlreadyGuarded = 0.0f;
			AlreadyGuarded--;
		}
#endif // WITH_SERVER
	}
};
