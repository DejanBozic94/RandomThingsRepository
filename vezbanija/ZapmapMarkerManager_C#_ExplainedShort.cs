class ZapmapMarkerManager {
    Array chargepointLocationKeys;
    int thinningCounter;
    int thinningMod;
    Point centerPoint;
    int pointCountInBonds;
    Dictionary visibleMarkers;
    Dictionary visibleLocationKeys;
    int lastZoomLevel;
    double thinningDistance;
    bool thinned;
    bool showMarkerSetting;

// 1 - id init() -> inicijalizacija, alokacija memorije za promenljive

// 2 - id initWithLocationKeys(Array locKeys) -> inicijalizacija, chargepointLocationKeys bude pokazivac na niz lockKeys koji se sad moze menjati po potrebi

// 3 - void updateLocatinKeys(Array locKeys) -> update-ovanje locationKeys

// 4 - void filter(Array filteredLocationKeys) -> brise sve objekte iz Array chargepointLocationKeys
//                                             -> uklanja sve vidljive markere - this.removeAllVisibleMarkers();
//                                             -> chargePointLocationKeys pokazuju sad na filteredLocationKeys
//                                             -> udate-uje markere - this.updateMarkers();

void filter(Array filteredLocationKeys) {
    
    chargePointLocationKeys.removeAllObjects();
    this.removeAllVisibleMarkers();
    chargePointLocationKeys = filteredLocationKeys;
    this.updateMarkers();
}

// 5 - uklanja sve vidljive markere, tj one koji su nacrtani
void removeAllVisibleMarkers() {
    
    Dictionary copyOfVisibleLoctionKeys = visibleLocationKeys; // napravljena kopija zbog lakseg handleovanja
    foreach(id key in copyOfVisibleLoctionKeys) {               // prodjemo kroz svaki objekat Dictionary-ja
        this.removeMarkerIfDrawn(visibleLocationKeys[key]);     // pozivamo metodu za uklanjanje nacrtanih markera
    }
}

// 6 - void updateTaxiOnlySetting() - update-uje neki taxi setting

// 7 - ukljanja markere koji su nacrtani, kada se prosledi locationKey
void removeMarkerIfDrawn(ChargepointLocationKey* locationKey) {
    
    if(visibleMarkers[locationKey.keyId]){                          // ako u Dictionary-ju visibleMarkers postoji marker sa prosledjenim locationKey
    
        Marker* marker = visibleMarkers[locationKey.keyId];         // taj marker dodeli promenljivi marker
        marker.map = null;                                          // stavi da ne pokazuje ni na jednu mapu
        visibleMarkers.removeObjectForKey(locationKey.keyId);       // izbaci taj marker iz Dictionary-ja visibleMarkers
        visibleLocationKeys.removeObjectForKey(locationKey.keyId);  // izbaci objekat sa tim kljuciem iz Dictionary-ja visibleLocationKeys
    }
}

// 8 - resetuje 'promenljive'
private void resetDefaults() {
    
    thinningCounter = 0;                                            
    thinningMod = 30;
    
    if(_chargepointMapView) {                                       // ako postoji _chargepointMapView, nisam bas siguran sta je ovo
        centerPoint = _chargepointMapView.camera.target;            // centralna tacka ce biti ona gde je kamera trenutno, pa ce naci centar od toga
        lastZoomLevel = (int)_chargepointMapView.camera.zoom;       // zoomLevel ce dobiti vrednost iz camera.zoom
    }
    pointCountInBonds = this.pointSizeInRegion();                   // broj tacaka u okviru ce se dobiti iz metode this.pointSizeInRegion();

    if(MapUtil.isInLondon(centerPoint)){                            // ako je centralna tacka u Londonu
        thinningDistance = 2;                                       
    }
    else {                                                          // ako centralna tacka nije u Londonu
        thinningDistance = 20;
    }

}

// 9 - fja koja crta svaki 30. marker koliko sam skontao
void drawThinnedMarker(ChargepointLocationKey* locationKey) {
    
    Projection* currentProjection = _chargepointMapView.projection;                 // dobijem trenutnu projekciju
    if(thinningCounter % thinningMod == 0) {                                        // thinningCounter se inkrementira u metodi updateMarkers() ili se resetuje na 0
                                                                                    // thinningMode je uvek 30, sto znaci da ce kad thinning counter bude 0, 30, 60, 90... uci u if
        if(locationKey.priority > 0) {                                              // ako je prioritet markera setovana na 1
            this.drawMarkerOnMap(locationKey, false);                               // nacrtaj marker pomocu metode this.drawMarkerOnMap(locationKey, false); ovo false znaci da je increaseSize false
        }
    }
    else {                                                                          // u slucaju kad thinningCounter nije 0, 30, 60, 90...
        if(lastZoomLevel > MINIMUM_ZOOM_LEVEL_FOR_PANNING){ // zoom ide od 4 do 12  // prvo provera da li je zoom veci od 5, ako jeste
            if(!currentProjection.containsCoordinate(locationKey.coordinate)){      // proverimo da li trenutna projekcija sadrzi koordinate markera
                this.removeMarkerIfDrawn(locationKey);                              // ako ne sadrzi koordinate, obrisi marker ako je nacrtan
            }
        }
        else {                                                                      // slucaj kada je zoom 4 ili 5, znaci vidi se cela V.Britanija
            this.removeMarkerIfDrawn(locationKey);                                  // ukloni marker
        }
    }
}

// 10 - fja koja crta gusce markere ako sam dobro skontao
void drawMarkerWithDense(ChargepointLocationKey* locationKey) {
    Location* keyLocation = (locationKey.longitude, locationKey.latitude);
    Location* centarLocation = (centarPoint.longitude, centarPoint.latitude);

    double distance = keyLocation.distanceFromLocation(centarLocation);             // distanca izmedju locationKey i centarPoint
    
    if (distance <= thinningDistance) {                                             // ako je distanca <= od thinningDistance, koji inace moze biti 2 ako je centarPoint u Londonu, a ako nije, onda je 20
        this.drawMarkerOnMap(locationKey, false);                                   // nacrtaj marker na mapu
    }
    else{
        this.drawThinnedMarker(locationKey);                                        // u suprotnom, nacrtaj marker ali istanjenom metodom
    }

}

// 11 - void removeUnusedMarkersFromCache() - uklanja markere koji se ne koriste iz kes memorije

// 12 - metoda koja trigeruje update
void triggerUpdate() {
    if(chargePointLocationKeys) {
        this.@delegate.markersDidStartUpdating();
        this.updateMarkers();
    }
    lastZoomLevel = (int)_chargepointMapView.camera.zoom;
    this.@delegate.centerDidChange(_chargepointMapView.camera.target);
}

// 13 - metoda za updateovanje markera na mapi
void updateMarkers() {

    this.removeUnusedMarkersFromCache();                                    // uklonimo markere koji se ne koriste iz kesa
    this.resetDefaults();                                                   // reset promenljivih, thinningCounter = 0, thinningMod = 30, thinningDistance = 2 ako (centar == London) ili thinningDistance = 30, i pointSizeInBounds dobijemo broj
    thinned = false;

    Projection* currentProjection = _chargepointMapView.projection;         // trenutna projekcija
    double zoomLevel = _chargepointMapView.camera.zoom;                     // zoom imamo

    foreach(ChargepointLocationKey locationKey in chargePointLocationKeys){ // za svaku lokaciju iz niza lokacija
        
        thinningCounter++;                                                  // uvecamo thinningCounter

        if(currentProjection.containsCoordinate(locationKey.coordinate)){   // ako trenutna projekcija sadrzi koordinate lokacije
            if(zoomLevel >= MINIMUM_ZOOM_LEVEL_FOR_PANNING){                    // ako je zoom >= 5
                if(pointCountInBonds <= MAX_MARKER_TO_SHOW_IN_BOUNDS) {             // ako broj tacaka u projekciji <= 100
                    this.drawMarkerOnMap(locationKey, false);                           // nacrtaj marker na mapu, normalno
                }
                else {                                                              // ako broj tacka u projekciji > 100
                    this.drawMarkerWithDense(locationKey);                              // nacrtaj marker na mapu ali preko gusce fje
                    thinned = true;
                }
            }
            else {                                                              // ako je zoom < 5
                if(pointCountInBonds <= MAX_MARKER_TO_SHOW_IN_BOUNDS){              // ako broj tacaka u projekciji <= 100
                    this.drawMarkerOnMap(locationKey, false);                           // nacrtaj marker na mapu, normalno
                }
                else {                                                              // ako broj tacka u projekciji > 100
                    this.drawThinnedMarker(locationKey);                                // nacrtaj marker na mapu ali preko tanje fje
                    thinned = true;
                }
            }

        }
        else {                                                              // ako trenutna projekcija NE sadrzi koordinate lokacije
            this.removeMarkerIfDrawn(locationKey);                              // izbrisati marker
        }
    }
    this.notifyDelegate();

}

// 14 - obavesti delegata
void notifyDelegate() {
    _delegate.didUpdateMarkers(visibleMarkers, thinned); 
}

// 15 - int pointSizeInRegion() - vraca broj tacaka u odredjenoj projekciji (regiji)

// 16 - void drawMarkerOnMap(ChargepointLocationKey* locationKey, bool increaseSize) - crta marker na mapi

// 17 - void loadMarkerIcon(Marker* marker, ChargepointLocationKey* locationKey, bool increaseSize)


}