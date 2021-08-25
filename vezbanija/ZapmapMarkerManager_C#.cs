[Category]
public class ZapmapMarkerManagerCategoryCategory : ZapmapMarkerManager
{
	public NSMutableDictionary markerImageCache { get; set; }
}

class ZapmapMarkerManager
{
	private NSMutableArray chargepointLocationKeys;
	private int thinningCounter;
	private int thinningMod;
	private CLLocationCoordinate2D centerPoint;
	private int pointCountInBounds;
	private NSMutableDictionary visibleMarkers;
	private NSMutableDictionary visibleLocationKeys;
	private int lastZoomLevel;
	private double thinningDistance;
	private bool thinned;
	private bool showMarkerSetting;

	private id init()
	{
		this = base.init();
		if (this != null)
		{
			chargepointLocationKeys = NSMutableArray.alloc().init();
			visibleLocationKeys = NSMutableDictionary.alloc().init();
			visibleMarkers = NSMutableDictionary.alloc().init();
			_markerImageCache = NSMutableDictionary.alloc().init();
			showMarkerSetting = SettingsManager.sharedManager().showsTaxiOnlyFilter();
			NSNotificationCenter.defaultCenter().addObserver(this) selector(__selector(updateTaxiOnlySetting)) name("showTaxiOnlySettingChanged") @object(null);
		}
		return this;
	}

	private id initWithLocationKeys(NSArray locKeys)
	{
		this = this.init();
		if (this != null)
		{
			chargepointLocationKeys = locKeys.mutableCopy();
		}
		return this;
	}

	private void updateLocationKeys(NSMutableArray locKeys)
	{
		chargepointLocationKeys = locKeys;
	}

	private void filter(NSMutableArray filteredLocationKeys)
	{
		chargepointLocationKeys.removeAllObjects();
		this.removeAllVisibleMarkers();
		chargepointLocationKeys = filteredLocationKeys;
		this.updateMarkers();
	}

	private void removeAllVisibleMarkers()
	{
		NSMutableDictionary copyOfVLK = NSMutableDictionary.alloc().initWithDictionary(visibleLocationKeys);
		foreach (id key in copyOfVLK)
			this.removeMarkerIfDrawn(visibleLocationKeys[key]);
	}

	private void updateTaxiOnlySetting()
	{
		showMarkerSetting = SettingsManager.sharedManager().showsTaxiOnlyFilter();
	}

	private void removeMarkerIfDrawn(ChargepointLocationKey* locationKey)
	{
		if (visibleMarkers[locationKey.keyId])
		{
			GMSMarker* marker = visibleMarkers[locationKey.keyId];
			marker.map = null;
			visibleMarkers.removeObjectForKey(locationKey.keyId);
			visibleLocationKeys.removeObjectForKey(locationKey.keyId);
		}
	}

	private void drawThinnedMarker(ChargepointLocationKey* locationKey)
	{
		GMSProjection* currentProjection = _chargepointMapView.projection;
		if (thinningCounter % thinningMod == 0)
		{
			if (locationKey.priority > 0)
			{
				this.drawMarkerOnMap(locationKey) increaseSize(false);
			}
		}
		else
		{
			if (lastZoomLevel > MINIMUM_ZOOM_LEVEL_FOR_PANNING)
			{
				if (!currentProjection.containsCoordinate(locationKey.coordinate))
				{
					this.removeMarkerIfDrawn(locationKey);
				}
			}
			else
			{
				this.removeMarkerIfDrawn(locationKey);
			}
		}
	}

	private void drawMarkerWithDense(ChargepointLocationKey* locationKey)
	{
		CLLocation* keyLocation = CLLocation.alloc().initWithLatitude(locationKey.latitude) longitude(locationKey.longitude);
		CLLocation* centerLocation = CLLocation.alloc().initWithLatitude(centerPoint.latitude) longitude(centerPoint.longitude);
		double distance = keyLocation.distanceFromLocation(centerLocation);
		if (distance <= thinningDistance)
		{
			this.drawMarkerOnMap(locationKey) increaseSize(false);
		}
		else
		{
			this.drawThinnedMarker(locationKey);
		}
	}

	private void resetDefaults()
	{
		thinningCounter = 0;
		thinningMod = 30;
		if (_chargepointMapView)
		{
			centerPoint = _chargepointMapView.camera.target;
			lastZoomLevel = (_chargepointMapView.camera.zoom as int);
		}
		pointCountInBounds = this.pointSizeInRegion();
		if (MapUtil.isInLondon(centerPoint))
		{
			thinningDistance = 2;
		}
		else
		{
			thinningDistance = 20;
		}
	}

	private void removeUnusedMarkersFromCache()
	{
		NSMutableDictionary copyOfVLK = NSMutableDictionary.alloc().initWithDictionary(visibleLocationKeys);
		foreach (NSString key in copyOfVLK)
		{
			ChargepointLocationKey* locationKey = copyOfVLK[key];
			if (!chargepointLocationKeys.containsObject(locationKey))
			{
				this.removeMarkerIfDrawn(locationKey);
			}
		}
	}

	private void triggerUpdate()
	{
		if (chargepointLocationKeys)
		{
			this.@delegate.markersDidStartUpdating();
			this.updateMarkers();
		}
		lastZoomLevel = (_chargepointMapView.camera.zoom as int);
		this.@delegate.centerDidChange(_chargepointMapView.camera.target);
	}

	private void updateMarkers()
	{
		this.removeUnusedMarkersFromCache();
		this.resetDefaults();
		thinned = false;
		GMSProjection* currentProjection = _chargepointMapView.projection;
		double zoomLevel = _chargepointMapView.camera.zoom;
		foreach (ChargepointLocationKey locationKey in chargepointLocationKeys)
		{
			inc(thinningCounter);
			if (currentProjection.containsCoordinate(locationKey.coordinate))
			{
				if (zoomLevel >= MINIMUM_ZOOM_LEVEL_FOR_PANNING)
				{
					if (pointCountInBounds <= MAX_MARKER_TO_SHOW_IN_BOUNDS)
					{
						this.drawMarkerOnMap(locationKey) increaseSize(false);
					}
					else
					{
						this.drawMarkerWithDense(locationKey);
						thinned = true;
					}
				}
				else
				{
					if (pointCountInBounds <= MAX_MARKER_TO_SHOW_IN_BOUNDS)
					{
						this.drawMarkerOnMap(locationKey) increaseSize(false);
					}
					else
					{
						this.drawThinnedMarker(locationKey);
						thinned = true;
					}
				}
			}
			else
			{
				this.removeMarkerIfDrawn(locationKey);
			}
		}
		this.notifyDelegate();
	}

	private void notifyDelegate()
	{
		_delegate.didUpdateMarkers(visibleMarkers) thinned(thinned);
	}

	private int pointSizeInRegion()
	{
		int size = 0;
		foreach (ChargepointLocationKey locationKey in chargepointLocationKeys)
		{
			GMSProjection* currentProjection = _chargepointMapView.projection;
			if (currentProjection.containsCoordinate(locationKey.coordinate))
			{
				inc(size);
			}
		}
		return size;
	}

	private void drawMarkerOnMap(ChargepointLocationKey* locationKey) increaseSize(bool increaseSize)
	{
		if (!locationKey.showDefault && !SettingsManager.sharedManager().showsTaxiOnlyFilter())
		{
			this.removeMarkerIfDrawn(locationKey);
			return;
		}
		if (!visibleMarkers[locationKey.keyId])
		{
			GMSMarker* marker = GMSMarker.alloc().init();
			marker.position = locationKey.coordinate;
			visibleMarkers[locationKey.keyId] = marker;
			visibleLocationKeys[locationKey.keyId] = locationKey;
			marker.userData = locationKey;
			marker.tracksInfoWindowChanges = true;
			marker.map = _chargepointMapView;
			marker.zIndex = (increaseSize ? 2 : 1);
			this.loadMarkerIcon(marker) andLocationKey(locationKey) increaseSize(increaseSize);
		}
	}

	private void loadMarkerIcon(GMSMarker* marker) andLocationKey(ChargepointLocationKey* key) increaseSize(bool increaseSize)
	{
		NSString link = ImageCacher.sharedCacher().imageUrlStringByMarker(key.mapMarker);
		int imageWidth = ((increaseSize ? 30 * 1.32 : 30) as int);
		if (this.markerImageCache[link])
		{
			UIImage* cachedImage = this.markerImageCache[link];
			marker.icon = ImageUtils.imageWithImage(cachedImage) scaledToWidth(imageWidth);
			return;
		}
		AFHTTPSessionManager* manager = AFHTTPSessionManager.manager();
		manager.responseSerializer = AFImageResponseSerializer.serializer();
		if (link)
		{
			manager.GET(link) parameters(null) progress(null) success(( task,  responseObject) => {
				UIImage* image = (responseObject as UIImage);
				UIImage* scaledImage = ImageUtils.imageWithImage(image) scaledToWidth(imageWidth);
				marker.icon = scaledImage;
				this.markerImageCache[link] = scaledImage;
			}) failure(( task,  error) => {
				NSLog("Load Image error - %@", error.description());
			});
		}
	}

	private void redrawMarkerWithLocationKey(ChargepointLocationKey* locationKey) increaseSize(bool increaseSize)
	{
		this.removeMarkerIfDrawn(locationKey);
		this.drawMarkerOnMap(locationKey) increaseSize(increaseSize);
	}
}
