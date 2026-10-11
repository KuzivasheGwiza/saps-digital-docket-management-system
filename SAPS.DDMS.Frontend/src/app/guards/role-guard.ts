import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth';
import { hasPermission } from '../security/role-policy';
import { ToastrService } from 'ngx-toastr';

export const roleGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const toastr = inject(ToastrService, { optional: true });
  const requiredRoles = (route.data['roles'] as Array<string> | undefined) ?? [];
  const deniedRoles = (route.data['deniedRoles'] as Array<string> | undefined) ?? [];
  const requiredPermission = route.data['permission'] as string | undefined;
  const user = authService.getCurrentUser();
  
  if (!user) {
    return router.createUrlTree(['/login']);
  }

  if (deniedRoles.includes(user.roleName)) {
    toastr?.warning(
      `Access denied because the ${user.roleName} role does not perform docket operations.`,
      'Access restricted'
    );
    return router.createUrlTree(['/dashboard']);
  }
  
  const roleAllowed = requiredRoles.some(role => user.roleName === role);
  const permissionAllowed = requiredPermission
    ? hasPermission(user.permissions, requiredPermission)
    : false;

  if ((requiredRoles.length > 0 || requiredPermission) && !roleAllowed && !permissionAllowed) {
    const roleReason = requiredRoles.length
      ? `This page is restricted to: ${requiredRoles.join(', ')}.`
      : `This page requires the ${requiredPermission} permission.`;
    toastr?.warning(`Access denied because your ${user.roleName} role is not authorised. ${roleReason}`, 'Access restricted');
    return router.createUrlTree(['/dashboard']);
  }
  
  return true;
};
