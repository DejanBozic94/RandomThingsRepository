//
// Created by Ikbal Kaya on 24/04/2017.
// Copyright (c) 2017 Ikbal Kaya. All rights reserved.
//
​
#import "ZapmapMarkerManager.h"
#import "ChargepointLocationKey.h"
#import "ImageCacher.h"
#import "MapUtil.h"
#import "GoogleMaps/GMSProjection.h"
#import "GoogleMaps/GoogleMaps.h"
#import "AFURLResponseSerialization.h"
#import "AFHTTPSessionManager.h"
#import "Marker.h"
#import "ImageUtils.h"
#import "SettingsManager.h"
​
#define MAX_MARKER_TO_SHOW_IN_BOUNDS 100
#define MINIMUM_ZOOM_LEVEL_FOR_PANNING 5
​
@interface ZapmapMarkerManager ()
​
@property (nonatomic, strong) NSMutableDictionary *markerImageCache;
​
@end
​
@implementation ZapmapMarkerManager {
    NSMutableArray *chargepointLocationKeys;
    int thinningCounter;
    int thinningMod;
    CLLocationCoordinate2D centerPoint;
    int pointCountInBounds;
    NSMutableDictionary *visibleMarkers;
    NSMutableDictionary *visibleLocationKeys;
    int lastZoomLevel;
    double thinningDistance;
    BOOL thinned;
    BOOL showMarkerSetting;
}
​
- (id)init {
    self = [super init];
    if (self) {
        chargepointLocationKeys = [[NSMutableArray alloc] init];
        visibleLocationKeys = [[NSMutableDictionary alloc] init];
        visibleMarkers = [[NSMutableDictionary alloc] init];
        _markerImageCache = [[NSMutableDictionary alloc] init];
        showMarkerSetting = [[SettingsManager sharedManager] showsTaxiOnlyFilter];
        [[NSNotificationCenter defaultCenter] addObserver:self selector:@selector(updateTaxiOnlySetting) name:@"showTaxiOnlySettingChanged" object:nil];
    }
    return self;
}
- (id)initWithLocationKeys:(NSArray *)locKeys {
    self = [self init];
    if (self) {
        chargepointLocationKeys = [locKeys mutableCopy];
    }
    return self;
}
​
- (void)updateLocationKeys:(NSMutableArray *)locKeys {
    chargepointLocationKeys = locKeys;
}
​
- (void)filter:(NSMutableArray *)filteredLocationKeys {
    [chargepointLocationKeys removeAllObjects];
    [self removeAllVisibleMarkers];
    chargepointLocationKeys = filteredLocationKeys;
    [self updateMarkers];
}
​
- (void)removeAllVisibleMarkers {
    NSMutableDictionary *copyOfVLK = [[NSMutableDictionary alloc] initWithDictionary:visibleLocationKeys];
    for (id key in copyOfVLK) {
        [self removeMarkerIfDrawn:visibleLocationKeys[key]];
    }
}
​
-(void)updateTaxiOnlySetting{
    showMarkerSetting = [[SettingsManager sharedManager] showsTaxiOnlyFilter];
}
- (void)removeMarkerIfDrawn:(ChargepointLocationKey *)locationKey {
    if (visibleMarkers[@(locationKey.keyId)]) {
        GMSMarker *marker = visibleMarkers[@(locationKey.keyId)];
        marker.map = nil;
        [visibleMarkers removeObjectForKey:@(locationKey.keyId)];
        [visibleLocationKeys removeObjectForKey:@(locationKey.keyId)];
    }
}
​
- (void)drawThinnedMarker:(ChargepointLocationKey *)locationKey {
​
    GMSProjection *currentProjection = _chargepointMapView.projection;
    if ((thinningCounter % thinningMod) == 0) {
        if (locationKey.priority > 0) {
            [self drawMarkerOnMap:locationKey increaseSize:NO];
        }
    } else {
        if (lastZoomLevel > MINIMUM_ZOOM_LEVEL_FOR_PANNING) {
            if (![currentProjection containsCoordinate:locationKey.coordinate]) {
                [self removeMarkerIfDrawn:locationKey];
            }
        } else {
            [self removeMarkerIfDrawn:locationKey];
        }
    }
​
}
​
- (void)drawMarkerWithDense:(ChargepointLocationKey *)locationKey {
    CLLocation *keyLocation = [[CLLocation alloc]
            initWithLatitude:locationKey.latitude longitude:locationKey.longitude];
    CLLocation *centerLocation = [[CLLocation alloc]
            initWithLatitude:centerPoint.latitude longitude:centerPoint.longitude];
​
    double distance = [keyLocation distanceFromLocation:centerLocation];
    if (distance <= thinningDistance) {
        [self drawMarkerOnMap:locationKey increaseSize:NO];
    } else {
        [self drawThinnedMarker:locationKey];
    }
}
​
- (void)resetDefaults {
    thinningCounter = 0;
    thinningMod = 30;
​
    if (_chargepointMapView) {
        centerPoint = _chargepointMapView.camera.target;
        lastZoomLevel = (int) _chargepointMapView.camera.zoom;
    }
    pointCountInBounds = [self pointSizeInRegion];
    if ([MapUtil isInLondon:centerPoint]) {
        thinningDistance = 2;
    } else {
        thinningDistance = 20;
    }
​
}
​
- (void)removeUnusedMarkersFromCache {
    NSMutableDictionary *copyOfVLK = [[NSMutableDictionary alloc] initWithDictionary:visibleLocationKeys];
​
    for (NSString *key in copyOfVLK) {
        ChargepointLocationKey *locationKey = copyOfVLK[key];
        if (![chargepointLocationKeys containsObject:locationKey]) {
            [self removeMarkerIfDrawn:locationKey];
        }
    }
}
​
- (void)triggerUpdate {
​
    if (chargepointLocationKeys) {
        [self.delegate markersDidStartUpdating];
        [self updateMarkers];
    }
    lastZoomLevel = (int) _chargepointMapView.camera.zoom;
    [self.delegate centerDidChange:_chargepointMapView.camera.target];
​
}
​
//run this in background thread and cancel if needed
- (void)updateMarkers {
​
    [self removeUnusedMarkersFromCache];
    [self resetDefaults];
    thinned = NO;
    GMSProjection *currentProjection = _chargepointMapView.projection;
    double zoomLevel = _chargepointMapView.camera.zoom;
​
    for (ChargepointLocationKey *locationKey in chargepointLocationKeys) {
        thinningCounter++;
        if ([currentProjection containsCoordinate:locationKey.coordinate]) {
            if (zoomLevel >= MINIMUM_ZOOM_LEVEL_FOR_PANNING) {
                if (pointCountInBounds <= MAX_MARKER_TO_SHOW_IN_BOUNDS) {
                    [self drawMarkerOnMap:locationKey increaseSize:NO];
                } else {
                    [self drawMarkerWithDense:locationKey];
                    thinned = YES;
                }
            } else {
                if (pointCountInBounds <= MAX_MARKER_TO_SHOW_IN_BOUNDS) {
                    [self drawMarkerOnMap:locationKey increaseSize:NO];
                } else {
                    [self drawThinnedMarker:locationKey];
                    thinned = YES;
                }
            }
        } else {
            [self removeMarkerIfDrawn:locationKey];
        }
​
    }
​
    [self notifyDelegate];
​
}
​
- (void)notifyDelegate {
    [_delegate didUpdateMarkers:visibleMarkers thinned:thinned];
}
​
- (int)pointSizeInRegion {
​
    int size = 0;
    for (ChargepointLocationKey *locationKey in chargepointLocationKeys) {
        GMSProjection *currentProjection = _chargepointMapView.projection;
        if ([currentProjection containsCoordinate:locationKey.coordinate]) {
            size++;
        }
    }
    return size;
}
​
- (void)drawMarkerOnMap:(ChargepointLocationKey *)locationKey increaseSize:(BOOL)increaseSize {
    if(!locationKey.showDefault && ![[SettingsManager sharedManager] showsTaxiOnlyFilter]){
        [self removeMarkerIfDrawn:locationKey];
        return;
    }
    if (!visibleMarkers[@(locationKey.keyId)]) {
        GMSMarker *marker = [[GMSMarker alloc] init];
​
        marker.position = locationKey.coordinate;
        visibleMarkers[@(locationKey.keyId)] = marker;
        visibleLocationKeys[@(locationKey.keyId)] = locationKey;
        marker.userData = locationKey;
        marker.tracksInfoWindowChanges = YES;
​
        marker.map = _chargepointMapView;
        marker.zIndex = increaseSize ? 2 : 1;
        [self loadMarkerIcon:marker andLocationKey:locationKey
                increaseSize:increaseSize];
    }
}
​
//- (void)redrawMarker
​
- (void)loadMarkerIcon:(GMSMarker *)marker andLocationKey:(ChargepointLocationKey *)key increaseSize:(BOOL)increaseSize {
    NSString *link = [[ImageCacher sharedCacher] imageUrlStringByMarker:key.mapMarker];
    int imageWidth = (int) (increaseSize ? 30 * 1.32 : 30);
    if (self.markerImageCache[link]) {
        UIImage *cachedImage = self.markerImageCache[link];
        marker.icon = [ImageUtils imageWithImage:cachedImage scaledToWidth:imageWidth];
        return;
    }
    AFHTTPSessionManager *manager = [AFHTTPSessionManager manager];
    manager.responseSerializer = [AFImageResponseSerializer serializer];
    if(link){
        [manager GET:link parameters:nil progress:nil success:^(NSURLSessionDataTask *task, id responseObject) {
            UIImage *image = (UIImage *) responseObject;
            UIImage *scaledImage = [ImageUtils imageWithImage:image scaledToWidth:imageWidth];
            marker.icon = scaledImage;
            self.markerImageCache[link] = scaledImage;
        } failure:^(NSURLSessionDataTask *task, NSError *error) {
            NSLog(@"Load Image error - %@", [error description]);
        }];
    }
}
​
- (void)redrawMarkerWithLocationKey:(ChargepointLocationKey *)locationKey
                       increaseSize:(BOOL)increaseSize {
    [self removeMarkerIfDrawn:locationKey];
    [self drawMarkerOnMap:locationKey increaseSize:increaseSize];
}
​
@end