// BED-172 — Drop-in grounded player pawn for Kayleigh (walk + prox-talk V1).
// Parent BP_KayleighCharacter to this class after plugin rebuild.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "InputActionValue.h"
#include "KayleighPlayerCharacter.generated.h"

class USpringArmComponent;
class UCameraComponent;
class UAudioCaptureComponent;
class UAudioComponent;
class UInputMappingContext;
class UInputAction;

/**
 * Grounded Kayleigh player character: capsule movement, eye-level camera, prox-talk audio path.
 * GameMode sets possession; AutoPossessPlayer is Disabled.
 */
UCLASS(Blueprintable)
class KAYLEIGHPLAYER_API AKayleighPlayerCharacter : public ACharacter
{
	GENERATED_BODY()

public:
	AKayleighPlayerCharacter();

protected:
	virtual void BeginPlay() override;
	virtual void SetupPlayerInputComponent(class UInputComponent* PlayerInputComponent) override;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Camera", meta = (AllowPrivateAccess = "true"))
	TObjectPtr<USpringArmComponent> CameraBoom;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Camera", meta = (AllowPrivateAccess = "true"))
	TObjectPtr<UCameraComponent> FollowCamera;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Audio|ProxTalk", meta = (AllowPrivateAccess = "true"))
	TObjectPtr<UAudioCaptureComponent> AudioCapture;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Audio|ProxTalk", meta = (AllowPrivateAccess = "true"))
	TObjectPtr<UAudioComponent> ProxVoice;

	/** When set, Move / Look / ProxTalk bind via Enhanced Input (IMC added on possess). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Input|Enhanced")
	TObjectPtr<UInputMappingContext> DefaultMappingContext;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Input|Enhanced")
	TObjectPtr<UInputAction> MoveAction;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Input|Enhanced")
	TObjectPtr<UInputAction> LookAction;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Input|Enhanced")
	TObjectPtr<UInputAction> ProxTalkAction;

	// Legacy axis fallbacks (used when IMC is not assigned or EI subsystem unavailable).
	void MoveForward(float Value);
	void MoveRight(float Value);
	void Turn(float Value);
	void LookUp(float Value);

	void OnMoveTriggered(const FInputActionValue& Value);
	void OnLookTriggered(const FInputActionValue& Value);
	void OnProxTalkStarted(const FInputActionValue& Value);
	void OnProxTalkCompleted(const FInputActionValue& Value);

	void StartProxTalk();
	void StopProxTalk();

	bool bIsProxTalking = false;
};
