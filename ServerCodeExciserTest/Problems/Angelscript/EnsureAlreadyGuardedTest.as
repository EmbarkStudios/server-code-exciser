class UTickerGuardedTest
{
	UFUNCTION(BlueprintOverride)
	void Tick(float DeltaSeconds)
	{
		FTraceScope TraceScope(n"UGaitGroundContactHelperComponent::Tick");
		check(System::IsServer());

#ifdef WITH_SERVER
		GroundContactHitResults.Empty();
#endif
	}
};
