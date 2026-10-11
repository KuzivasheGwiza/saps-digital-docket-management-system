// station-tree.ts — Complexity 1i: tree view | 1j: dynamic tree CRUD
// Tree: Province → Station → Docket count
import { Component, OnInit, inject, ChangeDetectorRef, ChangeDetectionStrategy, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { Subject, forkJoin, of } from 'rxjs';
import { catchError, takeUntil } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import { API_URL } from '../../config/api.config';
import { AuthService } from '../../services/auth';
import { DEFAULT_PROVINCES, ReferenceDataService } from '../../services/reference-data';
import { trimmedTextValidator } from '../../validation/input.validators';
import { getApiErrorMessage } from '../../validation/api-error';
import { DialogService } from '../../shared/dialog.service';

export interface ProvinceNode {
  province: string;
  expanded: boolean;
  stations: StationNode[];
  cities: string[];
}

export interface StationNode {
  id: number;
  name: string;
  province: string;
  region: string;
  docketCount: number;
  expanded: boolean;
  editing: boolean;
}

@Component({
  selector: 'app-station-tree',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterModule],
  templateUrl: './station-tree.html',
  styleUrls: ['./station-tree.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StationTree implements OnInit, OnDestroy {
  private http = inject(HttpClient);
  private authService = inject(AuthService);
  private referenceData = inject(ReferenceDataService);
  private fb = inject(FormBuilder);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  private dialog = inject(DialogService);
  private destroy$ = new Subject<void>();

  tree: ProvinceNode[] = [];
  isLoading = false;
  isSaving = false;
  showAddModal = false;
  showAddCityModal = false;
  showDeleteModal = false;
  selectedStation: StationNode | null = null;

  provinces = [...DEFAULT_PROVINCES];

  addForm: FormGroup;
  cityForm: FormGroup;
  editInlineForm: FormGroup;

  currentUser = this.authService.getCurrentUser();
  isAdmin = this.currentUser?.roleName === 'System Administrator';

  constructor() {
    this.addForm = this.fb.group({
      name:     ['', [trimmedTextValidator(3, 256)]],
      province: ['', [trimmedTextValidator(2, 50)]],
      region:   ['', [trimmedTextValidator(2, 50)]]
    });
    this.cityForm = this.fb.group({
      name: ['', [trimmedTextValidator(2, 50)]],
      province: ['', [trimmedTextValidator(2, 50)]]
    });
    this.editInlineForm = this.fb.group({
      name:   ['', [trimmedTextValidator(3, 256)]],
      region: ['', [trimmedTextValidator(2, 50)]]
    });
  }

  ngOnInit(): void {
    this.loadProvinceLookups();
    this.loadTree();
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadProvinceLookups(): void {
    this.referenceData.getOperationalLookups()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: lookups => {
          const merged = [...DEFAULT_PROVINCES, ...(lookups.provinces || [])];
          this.provinces = this.normaliseProvinces(merged);
          this.buildTreeFromCurrentStations();
          this.cdr.detectChanges();
        },
        error: () => {
          this.provinces = this.normaliseProvinces(DEFAULT_PROVINCES);
          this.buildTreeFromCurrentStations();
          this.cdr.detectChanges();
        }
      });
  }

  loadTree(): void {
    this.isLoading = true;
    this.cdr.detectChanges();
    forkJoin({
      stations: this.http.get<any[]>(`${API_URL}/user/stations`),
      cities: this.http.get<any[]>(`${API_URL}/user/cities`).pipe(catchError(() => of([])))
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: ({ stations, cities }) => { this.buildTree(stations, cities); this.isLoading = false; this.cdr.detectChanges(); },
        error: (err) => {
          this.tree = [];
          this.toastr.error(getApiErrorMessage(err, 'The station hierarchy could not be loaded'));
          this.isLoading = false;
          this.cdr.detectChanges();
        }
      });
  }

  buildTree(stations: unknown, managedCities: unknown = []): void {
    const safeStations = (Array.isArray(stations) ? stations : [])
      .filter(station => !!station && Number.isSafeInteger(Number(station.id)) && Number(station.id) > 0)
      .map(station => ({
        id: Number(station.id),
        name: typeof station.name === 'string' && station.name.trim() ? station.name.trim() : `Station ${station.id}`,
        province: typeof station.province === 'string' && station.province.trim() ? station.province.trim() : 'Unknown',
        region: typeof station.region === 'string' ? station.region.trim() : '',
        docketCount: Number.isSafeInteger(Number(station.docketCount)) && Number(station.docketCount) >= 0
          ? Number(station.docketCount)
          : 0
      }));
    const safeCities = (Array.isArray(managedCities) ? managedCities : [])
      .map(city => ({
        province: typeof city?.province === 'string' ? city.province.trim() : '',
        name: typeof city?.name === 'string' ? city.name.trim() : ''
      }))
      .filter(city => !!city.province && !!city.name);
    const knownProvinces = this.normaliseProvinces([
      ...this.provinces,
      ...safeStations.map(station => station.province),
      ...safeCities.map(city => city.province)
    ]);
    const map = new Map<string, { stations: StationNode[]; cities: string[] }>();
    for (const province of knownProvinces) {
      map.set(province, { stations: [], cities: [] });
    }
    for (const s of safeStations) {
      const prov = s.province;
      if (!map.has(prov)) map.set(prov, { stations: [], cities: [] });
      map.get(prov)!.stations.push({ id:s.id, name:s.name, province:s.province, region:s.region, docketCount:s.docketCount, expanded:false, editing:false });
    }
    for (const city of safeCities) {
      if (!map.has(city.province)) map.set(city.province, { stations: [], cities: [] });
      map.get(city.province)!.cities.push(city.name);
    }
    this.tree = Array.from(map.entries())
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([prov, entry]) => ({
        province: prov,
        expanded: true,
        stations: entry.stations.sort((left, right) => left.name.localeCompare(right.name)),
        cities: [...new Set(entry.cities)].sort((left, right) => left.localeCompare(right))
      }));
  }

  private buildTreeFromCurrentStations(): void {
    if (!this.tree.length) return;
    const currentStations = this.tree.flatMap(province =>
      province.stations.map(station => ({
        id: station.id,
        name: station.name,
        province: station.province,
        region: station.region,
        docketCount: station.docketCount
      }))
    );
    this.buildTree(currentStations, this.tree.flatMap(province => province.cities.map(name => ({ province: province.province, name }))));
  }

  private normaliseProvinces(provinces: Array<string | null | undefined>): string[] {
    return Array.from(new Set(
      provinces
        .map(province => province?.trim())
        .filter((province): province is string => !!province)
    )).sort((left, right) => left.localeCompare(right));
  }

  toggleProvince(node: ProvinceNode): void { node.expanded = !node.expanded; this.cdr.markForCheck(); }

  citiesForProvince(node: ProvinceNode): string[] {
    return [...new Set([...node.cities, ...node.stations.map(station => station.region?.trim() || 'Unspecified city')])]
      .sort((left, right) => left.localeCompare(right));
  }

  stationsForCity(node: ProvinceNode, city: string): StationNode[] {
    return node.stations.filter(station => (station.region?.trim() || 'Unspecified city') === city)
      .sort((left, right) => left.name.localeCompare(right.name));
  }

  // Complexity 1j — Add station (modifies tree structure)
  openAddModal(): void {
    if (!this.isAdmin) {
      this.toastr.error('A station cannot be added because only a System Administrator may change the hierarchy.');
      return;
    }
    this.addForm.reset(); this.showAddModal = true; this.cdr.markForCheck();
  }
  closeAddModal(): void { this.showAddModal = false; this.cdr.markForCheck(); }

  openAddCityModal(): void {
    if (!this.isAdmin) {
      this.toastr.error('A city cannot be added because only a System Administrator may change the hierarchy.');
      return;
    }
    this.cityForm.reset();
    this.showAddCityModal = true;
    this.cdr.markForCheck();
  }

  closeAddCityModal(): void { this.showAddCityModal = false; this.cdr.markForCheck(); }

  async editCity(node: ProvinceNode, city: string): Promise<void> {
    if (!this.isAdmin || this.isSaving) return;
    const name = await this.dialog.open({
      title: 'Edit City', message: `Rename ${city} in ${node.province}. Linked stations will move with the city.`,
      inputLabel: 'City name', inputType: 'text', initialValue: city, required: true,
      minLength: 2, maxLength: 50, confirmText: 'Continue'
    });
    if (!name) return;
    const province = await this.dialog.open({
      title: 'Select Province', message: 'Enter the province for this city.', inputLabel: 'Province',
      inputType: 'select', initialValue: node.province, required: true,
      options: this.provinces.map(value => ({ value, label: value })), confirmText: 'Save City'
    });
    if (!province) return;

    this.isSaving = true;
    this.http.put(`${API_URL}/user/cities`, {
      originalName: city, originalProvince: node.province, name: name.trim(), province
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving = false; this.toastr.success('City updated'); this.loadTree(); },
      error: err => { this.isSaving = false; this.toastr.error(getApiErrorMessage(err, 'The city could not be updated')); this.cdr.markForCheck(); }
    });
  }

  async deleteCity(node: ProvinceNode, city: string): Promise<void> {
    if (!this.isAdmin || this.isSaving) return;
    const confirmed = await this.dialog.confirm({
      title: 'Remove City', message: `Remove ${city} from ${node.province}? Cities with linked stations cannot be removed.`,
      confirmText: 'Remove City', tone: 'danger'
    });
    if (!confirmed) return;
    this.isSaving = true;
    this.http.delete(`${API_URL}/user/cities`, { params: { province: node.province, name: city } })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: () => { this.isSaving = false; this.toastr.success('City removed'); this.loadTree(); },
        error: err => { this.isSaving = false; this.toastr.error(getApiErrorMessage(err, 'The city could not be removed')); this.cdr.markForCheck(); }
      });
  }

  addCity(): void {
    if (!this.isAdmin) {
      this.toastr.error('A city cannot be added because only a System Administrator may change the hierarchy.');
      return;
    }
    if (this.isSaving) return;
    this.trimStationForm(this.cityForm);
    if (this.cityForm.invalid) {
      this.cityForm.markAllAsTouched();
      this.toastr.error('The city cannot be added because a valid city name and province are required.');
      return;
    }
    const dto = this.cityForm.getRawValue();
    this.isSaving = true;
    this.http.post<any>(`${API_URL}/user/cities`, dto)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.isSaving = false;
          this.toastr.success(`City "${dto.name}" added`);
          this.closeAddCityModal();
          this.loadTree();
        },
        error: err => {
          this.isSaving = false;
          this.toastr.error(getApiErrorMessage(err, 'The city could not be added'));
          this.cdr.markForCheck();
        }
      });
  }

  addStation(): void {
    if (!this.isAdmin) {
      this.toastr.error('A station cannot be added because only a System Administrator may change the hierarchy.');
      return;
    }
    if (this.isSaving) return;
    this.trimStationForm(this.addForm);
    if (this.addForm.invalid) {
      this.addForm.markAllAsTouched();
      this.toastr.error('The station cannot be added because a valid name, province and region are required.');
      return;
    }
    const dto = this.addForm.getRawValue();
    this.isSaving = true;
    this.http.post<any>(`${API_URL}/user/stations`, dto)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (created) => {
          this.isSaving = false;
          const createdId = Number(created?.id);
          if (!Number.isSafeInteger(createdId) || createdId <= 0) {
            this.toastr.success(`Station "${dto.name}" added`);
            this.closeAddModal();
            this.loadTree();
            return;
          }
          const newStation: StationNode = { id:createdId, name:dto.name, province:dto.province, region:dto.region, docketCount:0, expanded:false, editing:false };
          if (!this.provinces.includes(dto.province)) {
            this.provinces = this.normaliseProvinces([...this.provinces, dto.province]);
          }
          let prov = this.tree.find(p => p.province === dto.province);
          if (!prov) { prov = { province: dto.province, expanded: true, stations: [], cities: [dto.region] }; this.tree.push(prov); }
          if (!prov.cities.includes(dto.region)) prov.cities.push(dto.region);
          prov.stations.push(newStation);
          prov.stations.sort((left, right) => left.name.localeCompare(right.name));
          this.tree.sort((left, right) => left.province.localeCompare(right.province));
          this.toastr.success(`Station "${dto.name}" added`);
          this.closeAddModal();
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.isSaving = false;
          this.toastr.error(getApiErrorMessage(err, 'The station could not be added'));
          this.cdr.markForCheck();
        }
      });
  }

  // Complexity 1j — Edit station inline
  startEdit(station: StationNode): void {
    if (!this.isAdmin) {
      this.toastr.error('The station cannot be edited because only a System Administrator may change the hierarchy.');
      return;
    }
    this.tree.forEach(p => p.stations.forEach(s => s.editing = false));
    station.editing = true;
    this.editInlineForm.patchValue({ name: station.name, region: station.region });
    this.cdr.markForCheck();
  }

  saveEdit(station: StationNode): void {
    if (!this.isAdmin) {
      this.toastr.error('The station cannot be edited because only a System Administrator may change the hierarchy.');
      return;
    }
    if (this.isSaving) return;
    this.trimStationForm(this.editInlineForm);
    if (this.editInlineForm.invalid) {
      this.editInlineForm.markAllAsTouched();
      this.toastr.error('The station cannot be updated because a valid name and region are required.');
      return;
    }
    const { name, region } = this.editInlineForm.value;
    this.isSaving = true;
    this.http.put(`${API_URL}/user/stations/${station.id}`, { name, region, province: station.province })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.isSaving = false; station.name = name; station.region = region; station.editing = false; this.toastr.success('Station updated'); this.cdr.markForCheck(); },
        error: (err) => { this.isSaving = false; this.toastr.error(getApiErrorMessage(err, 'The station could not be updated')); this.cdr.markForCheck(); }
      });
  }

  cancelEdit(station: StationNode): void { station.editing = false; this.cdr.markForCheck(); }

  // Complexity 1j — Delete station
  openDeleteModal(station: StationNode): void { this.selectedStation = station; this.showDeleteModal = true; this.cdr.markForCheck(); }
  closeDeleteModal(): void { this.showDeleteModal = false; this.selectedStation = null; this.cdr.markForCheck(); }

  confirmDelete(): void {
    if (!this.isAdmin) {
      this.toastr.error('The station cannot be deleted because only a System Administrator may change the hierarchy.');
      return;
    }
    if (this.isSaving) return;
    if (!this.selectedStation) return;
    const id = this.selectedStation.id;
    const name = this.selectedStation.name;
    this.isSaving = true;
    this.http.delete(`${API_URL}/user/stations/${id}`)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.isSaving = false; this.removeFromTree(id, name); },
        error: (err) => { this.isSaving = false; this.toastr.error(getApiErrorMessage(err, 'The station could not be deleted')); this.cdr.markForCheck(); }
      });
  }

  private removeFromTree(id: number, name: string): void {
    this.tree.forEach(p => p.stations = p.stations.filter(s => s.id !== id));
    this.closeDeleteModal();
    this.toastr.success(`Station "${name}" removed`);
    this.cdr.markForCheck();
  }

  private trimStationForm(form: FormGroup): void {
    for (const name of ['name', 'province', 'region']) {
      const control = form.get(name);
      if (typeof control?.value === 'string') {
        control.setValue(control.value.trim(), { emitEvent: false });
      }
    }
  }
}
