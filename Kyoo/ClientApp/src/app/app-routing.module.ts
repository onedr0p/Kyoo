import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';

import { BrowseComponent } from './browse/browse.component';
import { ShowDetailsComponent } from './show-details/show-details.component';
import { NotFoundComponent } from './not-found/not-found.component';
import { ShowResolverService } from './services/show-resolver.service';
import { LibraryResolverService } from './services/library-resolver.service';


const routes: Routes = [
  { path: "browse", component: BrowseComponent, pathMatch: "full", resolve: { shows: LibraryResolverService } },
  { path: "browse/:library-slug", component: BrowseComponent, resolve: { shows: LibraryResolverService } },
  { path: "shows/:show-slug", component: ShowDetailsComponent, resolve: { show: ShowResolverService } },
  { path: "**", component: NotFoundComponent }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule],
  providers: [LibraryResolverService, ShowResolverService]
})
export class AppRoutingModule { }