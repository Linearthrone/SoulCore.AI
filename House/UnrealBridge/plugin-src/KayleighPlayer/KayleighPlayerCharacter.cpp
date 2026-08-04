// BED-172 — Kayleigh grounded player: walk, eye camera, prox-talk capture placeholder.

#include "KayleighPlayerCharacter.h"

#include "AudioCaptureComponent.h"
#include "Camera/CameraComponent.h"
#include "Components/AudioComponent.h"
#include "Components/CapsuleComponent.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/SpringArmComponent.h"
#include "InputAction.h"
#include "InputMappingContext.h"

AKayleighPlayerCharacter::AKayleighPlayerCharacter()
{
	PrimaryActorTick.bCanEverTick = false;

	// GameMode / placed instance handles possession.
	AutoPossessPlayer = EAutoReceiveInput::Disabled;

	GetCapsuleComponent()->InitCapsuleSize(34.f, 96.f);

	bUseControllerRotationYaw = false;

	if (UCharacterMovementComponent* MoveComp = GetCharacterMovement())
	{
		MoveComp->bOrientRotationToMovement = true;
		MoveComp->RotationRate = FRotator(0.f, 500.f, 0.f);
		MoveComp->MaxWalkSpeed = 450.f;
	}

	CameraBoom = CreateDefaultSubobject<USpringArmComponent>(TEXT("CameraBoom"));
	CameraBoom->SetupAttachment(GetCapsuleComponent());
	CameraBoom->SetRelativeLocation(FVector(0.f, 0.f, 64.f));
	CameraBoom->TargetArmLength = 0.f;
	CameraBoom->bUsePawnControlRotation = true;

	FollowCamera = CreateDefaultSubobject<UCameraComponent>(TEXT("FollowCamera"));
	FollowCamera->SetupAttachment(CameraBoom, USpringArmComponent::SocketName);
	FollowCamera->SetRelativeLocation(FVector(10.f, 0.f, 0.f));

	AudioCapture = CreateDefaultSubobject<UAudioCaptureComponent>(TEXT("AudioCapture"));

	ProxVoice = CreateDefaultSubobject<UAudioComponent>(TEXT("ProxVoice"));
	ProxVoice->SetupAttachment(GetCapsuleComponent());
	ProxVoice->bAutoActivate = false;
	ProxVoice->bAllowSpatialization = true;
}

void AKayleighPlayerCharacter::BeginPlay()
{
	Super::BeginPlay();

	static const FName KayleighPlayerTag(TEXT("KayleighPlayer"));
	if (!ActorHasTag(KayleighPlayerTag))
	{
		Tags.Add(KayleighPlayerTag);
	}
}

void AKayleighPlayerCharacter::SetupPlayerInputComponent(UInputComponent* PlayerInputComponent)
{
	Super::SetupPlayerInputComponent(PlayerInputComponent);

	// Legacy axis bindings — always available as fallback.
	PlayerInputComponent->BindAxis(TEXT("MoveForward"), this, &AKayleighPlayerCharacter::MoveForward);
	PlayerInputComponent->BindAxis(TEXT("MoveRight"), this, &AKayleighPlayerCharacter::MoveRight);
	PlayerInputComponent->BindAxis(TEXT("Turn"), this, &AKayleighPlayerCharacter::Turn);
	PlayerInputComponent->BindAxis(TEXT("LookUp"), this, &AKayleighPlayerCharacter::LookUp);

	if (DefaultMappingContext && MoveAction && LookAction && ProxTalkAction)
	{
		if (UEnhancedInputComponent* EnhancedInput = Cast<UEnhancedInputComponent>(PlayerInputComponent))
		{
			if (APlayerController* PC = Cast<APlayerController>(GetController()))
			{
				if (ULocalPlayer* LocalPlayer = PC->GetLocalPlayer())
				{
					if (UEnhancedInputLocalPlayerSubsystem* Subsystem =
						ULocalPlayer::GetSubsystem<UEnhancedInputLocalPlayerSubsystem>(LocalPlayer))
					{
						Subsystem->AddMappingContext(DefaultMappingContext, 0);
					}
				}
			}

			EnhancedInput->BindAction(MoveAction, ETriggerEvent::Triggered, this, &AKayleighPlayerCharacter::OnMoveTriggered);
			EnhancedInput->BindAction(LookAction, ETriggerEvent::Triggered, this, &AKayleighPlayerCharacter::OnLookTriggered);
			EnhancedInput->BindAction(ProxTalkAction, ETriggerEvent::Started, this, &AKayleighPlayerCharacter::OnProxTalkStarted);
			EnhancedInput->BindAction(ProxTalkAction, ETriggerEvent::Completed, this, &AKayleighPlayerCharacter::OnProxTalkCompleted);
		}
	}
}

void AKayleighPlayerCharacter::MoveForward(float Value)
{
	if (Controller != nullptr && !FMath::IsNearlyZero(Value))
	{
		const FRotator YawRotation(0.f, Controller->GetControlRotation().Yaw, 0.f);
		const FVector Direction = FRotationMatrix(YawRotation).GetUnitAxis(EAxis::X);
		AddMovementInput(Direction, Value);
	}
}

void AKayleighPlayerCharacter::MoveRight(float Value)
{
	if (Controller != nullptr && !FMath::IsNearlyZero(Value))
	{
		const FRotator YawRotation(0.f, Controller->GetControlRotation().Yaw, 0.f);
		const FVector Direction = FRotationMatrix(YawRotation).GetUnitAxis(EAxis::Y);
		AddMovementInput(Direction, Value);
	}
}

void AKayleighPlayerCharacter::Turn(float Value)
{
	AddControllerYawInput(Value);
}

void AKayleighPlayerCharacter::LookUp(float Value)
{
	AddControllerPitchInput(Value);
}

void AKayleighPlayerCharacter::OnMoveTriggered(const FInputActionValue& Value)
{
	const FVector2D MovementVector = Value.Get<FVector2D>();
	if (!FMath::IsNearlyZero(MovementVector.Y))
	{
		MoveForward(MovementVector.Y);
	}
	if (!FMath::IsNearlyZero(MovementVector.X))
	{
		MoveRight(MovementVector.X);
	}
}

void AKayleighPlayerCharacter::OnLookTriggered(const FInputActionValue& Value)
{
	const FVector2D LookVector = Value.Get<FVector2D>();
	if (!FMath::IsNearlyZero(LookVector.X))
	{
		AddControllerYawInput(LookVector.X);
	}
	if (!FMath::IsNearlyZero(LookVector.Y))
	{
		AddControllerPitchInput(LookVector.Y);
	}
}

void AKayleighPlayerCharacter::OnProxTalkStarted(const FInputActionValue& Value)
{
	if (Value.Get<bool>())
	{
		StartProxTalk();
	}
}

void AKayleighPlayerCharacter::OnProxTalkCompleted(const FInputActionValue& Value)
{
	StopProxTalk();
}

void AKayleighPlayerCharacter::StartProxTalk()
{
	if (bIsProxTalking)
	{
		return;
	}

	bIsProxTalking = true;

	// V1: start mic capture + spatial ProxVoice placeholder so attenuation path exists.
	// True mic -> world voice streaming may need VoiceModule / EOS / OnlineSubsystem wiring.
	if (AudioCapture)
	{
		AudioCapture->StartCapturingAudio();
	}

	if (ProxVoice && !ProxVoice->IsPlaying())
	{
		ProxVoice->Activate(true);
	}

	UE_LOG(LogTemp, Log, TEXT("KayleighPlayer: ProxTalk START (capture=%s proxVoice=%s)"),
		AudioCapture ? TEXT("on") : TEXT("missing"),
		ProxVoice ? TEXT("active") : TEXT("missing"));
}

void AKayleighPlayerCharacter::StopProxTalk()
{
	if (!bIsProxTalking)
	{
		return;
	}

	bIsProxTalking = false;

	if (AudioCapture)
	{
		AudioCapture->StopCapturingAudio();
	}

	if (ProxVoice)
	{
		ProxVoice->Deactivate();
	}

	UE_LOG(LogTemp, Log, TEXT("KayleighPlayer: ProxTalk STOP"));
}
