class UServerPostfixFunctionTest
{
	void Test_Server()
	{
		int SomethingBefore = 0;
		SomethingBefore++;

		if(System::IsServer())
		{
			float AlreadyGuarded = 0.0f;
			AlreadyGuarded--;
		}
	}
};
