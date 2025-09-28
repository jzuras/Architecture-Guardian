namespace ArchGuard.Shared;

// Interface for validation strategy pattern
public interface IValidationStrategy
{
    // ARCHGUARD_TEMPLATE_RULE_START
    Task<string> ValidateDependencyRegistrationAsync(ValidationRequest request);
    // ARCHGUARD_TEMPLATE_RULE_END

    // ARCHGUARD_INSERTION_POINT_METHODS_START
    // New rule method signatures go here in alphabetical order by rule name

    // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
    // Generated from template on: 9/17/25
    // DO NOT EDIT - This code will be regenerated
    Task<string> ValidateEntityDtoPropertyMappingAsync(ValidationRequest request);
    // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

    // ARCHGUARD_INSERTION_POINT_METHODS_END
}