import { FormControl, FormGroup } from '@angular/forms';
import { Modality } from '../../../domain/models';
import { SchoolingLevelDto } from '../../../api/schema';

/**
 * ADR (spec técnico §6.5): Reactive Forms tipados como estándar. El formulario de admisión es el
 * caso de mayor complejidad estructural del sistema (~30 campos, 5 grupos de dependencias
 * condicionales) — exactamente el escenario que la decisión razonada del spec describe.
 */
export interface PersonalGroup {
  fullName: FormControl<string>;
  birthDate: FormControl<string>;
  maritalStatus: FormControl<string>;
  email: FormControl<string>;
  phone: FormControl<string>;
}

export interface AddressGroup {
  street: FormControl<string>;
  neighborhood: FormControl<string>;
  locality: FormControl<string>;
  municipality: FormControl<string>;
  state: FormControl<string>;
}

export interface ChurchGroup {
  churchName: FormControl<string>;
  churchStreet: FormControl<string>;
  churchNeighborhood: FormControl<string>;
  churchLocality: FormControl<string>;
  churchMunicipality: FormControl<string>;
  pastorName: FormControl<string>;
  timeAttending: FormControl<string>;
  hasMinistryRole: FormControl<boolean>;
  ministryRoleName: FormControl<string>;
}

export interface EducationGroup {
  level: FormControl<SchoolingLevelDto>;
  otherDescription: FormControl<string>;
  theologicalBackground: FormControl<string>;
  studyPurpose: FormControl<string>;
}

export interface ModalityGroup {
  modality: FormControl<Modality>;
  requestedRegionId: FormControl<string>;
  onlineReason: FormControl<string>;
}

export interface AdmissionFormModel {
  personal: FormGroup<PersonalGroup>;
  address: FormGroup<AddressGroup>;
  church: FormGroup<ChurchGroup>;
  education: FormGroup<EducationGroup>;
  modality: FormGroup<ModalityGroup>;
}
